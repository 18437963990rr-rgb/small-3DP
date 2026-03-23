/**
    Copyright (c) 2023, Meteor Inkjet Limited

    SampleScanPrint demonstrates how to use the Meteor PrinterInterface
    API to:

    - start and connect to the Meteor PrintEngine
    - retrieve status information from Meteor
    - send control commands to Meteor
    - set up a scanning print job
*/
#include <stdio.h>
#include <chrono>
#include <thread>
#include <string.h>
#include "PrinterInterface.h"

// ---------------------------------------------------------------------------- //
// Global data
int g_bitsPerPixel = 0;
const char* g_cfgFile = nullptr;
uint32 g_scanCount = 10;
uint32 g_widthPx = 500;
uint32 g_heightPx = 500;


// Global data which needs to be synchronised over multiple threads
volatile bool g_exitThreads = false;
volatile bool g_headPowerBusy = false;
volatile bool g_allHeadsPowered = false;
volatile int g_controlCommand = -1;
volatile bool g_pccsPresent = false;
volatile bool g_startPrintJob = false;
volatile bool g_abortPrintJob = false;
volatile bool g_sendAbortCmd = false;


// ---------------------------------------------------------------------------- //
// Soak test mode (-t on the command line) continually starts jobs and triggers 
// scans.  This relies on Meteor being set up with an internal encoder and
// also assumes that each job has just 2 scans.
const int KSoakTestModeeOff             = 0;
const int KSoakTestModeStarting         = 1;
const int KSoakTestModeWaitingForScan1  = 2;
const int KSoakTestModeWaitingForScan2  = 3;
const int KSoakTestModeComplete         = 4;
volatile int g_soakTestState            = KSoakTestModeeOff;

// ---------------------------------------------------------------------------- //
// Definitions for the single character stdin commands which are recognised
// by the application
const char CMD_QUIT      = 'q';
const char CMD_HP_ON     = 'p';
const char CMD_HP_OFF    = 'o';
const char CMD_ABORT     = 'a';
const char CMD_FORCE_PD  = 'f';
const char CMD_START_JOB = 'j';

// ---------------------------------------------------------------------------- //
/**
    Prints the available commands to stdout
*/
void OutputCommandUsage() {
    printf("---- Commands are:             ----\n");
    printf("----   q: quit                 ----\n");
    printf("----   p: head power on        ----\n");
    printf("----   o: head power off       ----\n");
    printf("----   a: abort                ----\n");
    printf("----   f: force product detect ----\n");
    printf("----   j: start print job      ----\n");
}

// ---------------------------------------------------------------------------- //
/**
    Check whether all of the PCCs listed in the Meteor configuration file have
    connected.

    We check that PccsAttached is stable from five consecutive calls to 
    PiGetPrnStatus before deciding that all PCCs are present, because there are
    situations where PCCs can appear then briefly disappear during initialisation.
    
    For example, this can happen while the PCCs are negotiating their daisy chain 
    IDs, especially if some cards take slightly longer to boot up than others.

    In the opposite situation, when PccsAttached decreases, we can act as soon
    as the value changes, because it will likely indicate that there is 
    a problem (unless the PCCs have been deliberately powered off).
*/
void CheckPccsAttached(const TAppStatus* appStatus) {
    static int pccsPresentCount = 0;
    const int KPccsPresentStable = 5;

    if ( g_pccsPresent ) {
        if ( appStatus->PccsAttached != appStatus->PccsRequired ) {
            pccsPresentCount = 0;
            g_pccsPresent = false;
            printf("---- Pccs detached ----\n");
        }
    } else {
        if ( appStatus->PccsAttached == appStatus->PccsRequired ) {
            if ( ++pccsPresentCount == KPccsPresentStable ) {
                g_pccsPresent = true;
                printf("---- Pccs attached ----\n");
            }
        } else {
            pccsPresentCount = 0;
        }
    }
}

// ---------------------------------------------------------------------------- //
/**
    Meteor head power is "busy" if it is in the process of turning HDC and head
    power on or off.  
    
    While this is happening, Meteor cannot accept any more head power commands.

    This function checks the head power busy state for each PCC in the system.

    (Other PCC status bits, GPIO states etc. can be checked in a similar
     way - e.g. search for BMPS_* or BMPS2_* in Meteor.h to see the
     status bits which are available)
*/
eRET CheckHeadPowerBusy(const TAppStatus* appStatus) {
    bool headPowerBusy = false;
    for (int pccNum = 0; pccNum < appStatus->PccsAttached; pccNum++ ) {
        TAppPccStatus* pccStatus = PiGetPccStatus(pccNum + 1);
        if ( nullptr == pccStatus ) {
            return PiGetLastStatusError();
        }
        if ( pccStatus->bmStatusBits2 & BMPS2_HEAD_POWER_IN_PROGRESS ) {
            headPowerBusy = true;
        }
    }
    g_headPowerBusy = headPowerBusy;
    return RVAL_OK;
}

// ---------------------------------------------------------------------------- //
/**
    Iterate through each HDC in the system and note whether all HDCs are
    powered on or off.

    A similar loop can be used to query other HDC and print head
    related status.

    See TAppHeadStatus; the status fields which are available depend
    on the currently selected head type.
*/
eRET CheckHeadPowerState(const TAppStatus* appStatus) {
    bool allHeadsOn = true;
    bool allHeadsOff = true;
    //
    // Iterate each PCC in the system
    //
    for (int pccNum = 0; pccNum < appStatus->PccsAttached; pccNum++) {
        //
        // A PCC supports up to 8 physical or logical HDCs
        //
        // PDCs have one or more HDCs, and the PCC, combined into
        // one card; the HDCs are still independent logical entities
        // PDCs typically contain 1,2 or 4 HDCs, depending on the card type.
        //
        // The PCC status tells us the maximum number of HDCs supported on this PCC.
        //
        TAppPccStatus* pccStatus = PiGetPccStatus(pccNum + 1);
        if (nullptr == pccStatus) {
            allHeadsOn = false;
            allHeadsOff = false;
            continue;
        }
        //
        for (int hdcNum = 0; hdcNum < pccStatus->MaxHdcs; hdcNum++) {
            TAppHeadStatus* hdcStatus = PiGetHeadStatus(pccNum+1, hdcNum+1);
            if ( nullptr == hdcStatus ) {
                return PiGetLastStatusError();
            }
            //
            // If a head is listed in the [Planes] section of the Meteor
            // configuration file, the BMHS_HEAD_REQUIRED bit is set.
            // If a head is not listed, then it is not being used for
            // printing, so can be ignored
            //
            if ( !( hdcStatus->bmStatusBits & BMHS_HEAD_REQUIRED) ) {
                continue;
            }
            //
            // If both the BMHS_CFGEND and BMHS_VCCEN head status bits
            // are set, then the head is power up and has completed
            // any required initial configuration
            //
            if ( hdcStatus->bmStatusBits & (BMHS_CFGEND | BMHS_VCCEN) ) {
                allHeadsOff = false;
            } else {
                allHeadsOn = false;
            }
        }
    }
    if ( allHeadsOn ) {
        if ( !g_allHeadsPowered ) {
            printf("---- All Hdcs are on ----\n");
            g_allHeadsPowered = true;
        }
    } else if ( allHeadsOff ) {
        if ( g_allHeadsPowered ) {
            printf("---- All Hdcs are off ----\n");
            g_allHeadsPowered = false;
        }
    }
    return RVAL_OK;
}

// ---------------------------------------------------------------------------- //
/**
    Provided each status function is called from a single thread, the API is
    thread safe.  i.e. it is possible to have one thread for PiGetPrnStatus,
    a second thread for PiGetPccStatus etc.

    However, the calls themselves are not thread safe - e.g. there should not
    be multiple threads calling PiGetHeadStatus.

    (There is no performance advantage in doing this, but if it is necessary,
     PiGetStatusEx should be used for all status calls).
*/
void StatusThreadMain() {
    eRET rVal = RVAL_OK;

    while ( !g_exitThreads ) {

        //
        // It is possible for the status request to fail with
        // an error code of RVAL_BUSY. This means that the PrintEngine
        // is not able to service the request, and the application 
        // should try again later.
        //
        // This will be rare in an application with a single status
        // polling thread, but can occasionally happen at times of
        // heavy PrintEngine load - e.g. during a start job sequence
        // or while PCCs are connecting
        //
        // Other error codes should here happen here under normal
        // circumstances
        //
        if ( RVAL_OK != rVal ) {
            if ( RVAL_BUSY != rVal ) {
                printf("!!!! PiGetPrnStatus failed: err=%d !!!!\n", rVal);
                return;
            }
        }

        //
        // Retrieve and output any error messages from the Meteor PrintEngine.
        // These errors also appear in the Meteor log file.
        //
        // Meteor logging (including error messages) can also be sent directly to
        // the console if [Test] LogToConsole = 1 is set in the Meteor configuration
        // file.
        //
        // Often these errors are used to indicate a problem with how the application
        // is using Meteor - e.g. if print job commands are sent in the wrong sequence,
        // or if there are invalid parameters in config file, etc.
        //
        char meteorError[1024];
        while ( RVAL_OK == PiGetPrintEngineError(meteorError, sizeof(meteorError)) ) {
            printf("!!!! Meteor error: '%s' !!!!\n", meteorError);
        }

        //
        // The status from each PCC-E updates approximately every 300ms by default.
        // The status interval can be reduced in the Meteor configuration file, 
        // e.g. [Test] PcceStatusTimerMs = 50
        //
        std::this_thread::sleep_for(std::chrono::milliseconds(10));

        const TAppStatus* appStatus = PiGetPrnStatus();
        if ( nullptr == appStatus ) {
            rVal = PiGetLastStatusError();
            continue;
        }
        
        // ---- See if all of the required PCCs have attached ----
        CheckPccsAttached(appStatus);
        
        // ---- There's no point in looking at head status until ----
        // ---- all of the PCCs have connected                   ----
        if (!g_pccsPresent) {
            continue;
        }

        // ---- Count scans loaded ----
        static int scansLoaded = 0;
        if ( scansLoaded != appStatus->DocsQueuedLane1 ) {
            scansLoaded = appStatus->DocsQueuedLane1;
            printf("---- Scans loaded = %d ----\n", scansLoaded);

            if (KSoakTestModeStarting == g_soakTestState && scansLoaded > 1) {
                PiSetSignal(SIG_FORCEPD, 1);
                g_soakTestState = KSoakTestModeWaitingForScan1;
            }
        }

        // ---- Count scans printed ----
        static int scansPrinted = 0;
        if ( scansPrinted != appStatus->PrintCount ) {
            scansPrinted = appStatus->PrintCount;
            printf("---- Scans printed = %d ----\n", scansPrinted);

            if (KSoakTestModeWaitingForScan1 == g_soakTestState && scansPrinted == 1) {
                PiSetSignal(SIG_FORCEPD, 1);
                g_soakTestState = KSoakTestModeWaitingForScan2;
            }
            if (KSoakTestModeWaitingForScan2 == g_soakTestState && scansPrinted == 2) {
                g_soakTestState = KSoakTestModeComplete;
            }
        }

        // ---- Check whether head power is "busy" on any PCC in the system ----
        // ---- Head power is busy if the boards are in the process of      ----
        // ---- turning head power on or off                                ----
        if ( (rVal = CheckHeadPowerBusy(appStatus)) != RVAL_OK ) {
            continue;
        }

        // ---- Find out whether all heads are powered up ----
        if ( (rVal = CheckHeadPowerState(appStatus)) != RVAL_OK ) {
            continue;
        }
    }
}

// ---------------------------------------------------------------------------- //
/**
    Thread to send control commands to Meteor.

    These are the majority of commands in the PrinterInterface API aside from
    PiSendCommand, which is used to send print commands and data.
*/
void ControlThreadMain() {
    while ( !g_exitThreads ) {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
        if ( -1 == g_controlCommand && !g_sendAbortCmd ) {
            continue;
        }
        eRET rVal = RVAL_FAULT;
        bool sentAbort = false;
        if ( g_sendAbortCmd ) {
            sentAbort = true;
            rVal = PiAbort();
            printf("---- PiAbort returned %d ----\n", rVal);
        }
        //
        // Head power command
        //
        if (g_controlCommand == CMD_HP_ON || g_controlCommand == CMD_HP_OFF) {
            int state = g_controlCommand == CMD_HP_ON ? 1 : 0;
            rVal = PiSetHeadPower(state);
            printf("---- PiSetHeadPower(%d) returned %d ----\n", state, rVal);
            // If we get a busy because the PCCs aren't all connected, there's no point
            // in retrying.
            if ( !g_pccsPresent ) {
                g_controlCommand = -1;
            }
        }
        //
        // Force Product detect command.  On a scanning printer, this is the same
        // as setting the home position.
        //
        // If Meteor is set to run on internal clock (the [Encoder] PrintClock parameter
        // in the configuration file), then sending a Force PD can be used to simulate
        // a forwards scan.  If a scan is buffered, it will appear to print
        //
        if ( g_controlCommand == CMD_FORCE_PD ) {
            rVal = PiSetSignal(SIG_FORCEPD, 1);
            printf("---- PiSetSignal(SIG_FORCEPD, 1) returned %d ----\n", rVal);
        }
        //
        // On success, the control functions return RVAL_OK
        //
        // If Meteor is busy and cannot service the command then they return RVAL_BUSY
        // In this situation we must retry the command         ----
        // 
        // (A sleep is unnecessary because the PrinterInterface will already have yielded to 
        //   other threads)
        //
        if ( RVAL_OK == rVal ) {
            if ( sentAbort ) {
                g_sendAbortCmd = false;
            } else {
                g_controlCommand = -1;
            }
        } else if ( RVAL_BUSY != rVal ) {
            printf("!!!! Control command '%c' failed: err=%d !!!!\n", (char)g_controlCommand, rVal);
            return;
        }
    }
}

// ---------------------------------------------------------------------------- //
/**
    Set a maximum grey level pixel in the image buffer which is sent to
    Meteor.

    Note that this function is intended to demonstrate the layout of a
    Meteor image buffer.  Drawing an image via this function will not
    be the most efficient way of generating a Meteor print command.
*/
void SetPx(uint32_t x, uint32_t y, uint32_t bpp, uint32_t* imgBuf, uint32_t strideDwords) {
    //
    // Each line in the Meteor PCMD_IMAGE buffer is a whole number of 32-bit DWORDs
    // 
    uint32_t* lineStart = imgBuf + y*strideDwords;
    //
    // The number of pixels in each DWORD depends on the pixel bit depth
    //
    uint32_t pixelsPerDword = 32 / bpp;
    //
    // Pointer to the DWORD containing the pixel we're going to write
    //
    uint32_t* pixelDword = lineStart + (x / pixelsPerDword);
    //
    // Pixel data within each DWORD has the first (smallest X) pixel in the
    // most significant bit(s), and the last (largest X) pixel in the least
    // significant bit(s)
    //
    uint32_t mask = ((1<<bpp) - 1) << ( 32 - bpp * (1 + (x%pixelsPerDword)) );
    //
    // OR the pixel into the buffer.  
    // Here we are setting all of the pixel's bits (when bpp > 1), giving the 
    // largest drop size.  
    // Note that for heads running at four bits per pixel, this isn't always 
    // going to be correct, because often only the least significant three bits 
    // of pixel data are used when running at 4bpp.
    // 
    *pixelDword |= mask;
}

// ---------------------------------------------------------------------------- //
/**
    Create a PCMD_IMAGE command containing two one-pixel wide rectangles
    
    The outer rectangle is drawn around the outer border of the image
    
    An inner rectangle is drawn 'gap' pixels inside this
    
    i.e. if gap = 0, both rectangles are in the same place
         if gap = 1, we get a two-pixel wide rectangle
         if gap = 2, there is a one pixel wide gap between the rectangles
         etc.
*/
uint32_t* CreateImageCommand(uint32_t width, uint32_t height, uint32_t bpp, int gap) {
    //
    // The command head and image data are sent to Meteor in one buffer
    // The PCMD_IMAGE command has 6 header bytes
    //
    uint32_t hdrSizeDwords = 6;
    //
    // Each line of pixels in the image must start on a DWORD boundary
    //
    uint32_t lineLengthDwords = ((width * bpp) + 31) / 32;
    //
    // Total number of 32-bit DWORDs required for the image
    //
    uint32_t imageSizeDwords = height * lineLengthDwords;
    //
    // Total size of the command buffer (image + header)
    uint32_t cmdBufDwords = hdrSizeDwords + imageSizeDwords;

    //
    // Create the buffer, zero it, and write the command header into the buffer
    //
    // (Provided all of the image data will be written later, the memset with
    //  zero may not be necessary.  Here we need it so that the image is filled
    //  with "white" data (i.e. nothing printed) before we add the rectangles
    //
    uint32_t* cmdBuf = new uint32_t[cmdBufDwords];
    memset(cmdBuf, 0, cmdBufDwords * sizeof(uint32_t));
    uint32_t pos = 0;
    cmdBuf[pos++] = PCMD_IMAGE;     // -- Command
    cmdBuf[pos++] = cmdBufDwords-2; // -- Size of following command
    cmdBuf[pos++] = 1;              // -- Colour plane
    cmdBuf[pos++] = 1000;           // -- X start
    cmdBuf[pos++] = 128;            // -- Y top; 128 is chosen to avoid the feathered edge of a Samba head
    cmdBuf[pos++] = width;          // -- width in pixels

    //
    // Pointer to the start of the image within the command buffer
    //
    uint32_t* imgBuf = cmdBuf + pos;

    //
    // Draw a one pixel rectangle border around outside of the image
    //
    for ( uint32_t x = 0; x < width; x++ ) {
        SetPx(x, 0, bpp, imgBuf, lineLengthDwords);
        SetPx(x, height-1, bpp, imgBuf, lineLengthDwords);
    }
    for ( uint32_t y = 0; y < height; y++ ) {
        SetPx(0, y, bpp, imgBuf, lineLengthDwords);
        SetPx(width-1, y, bpp, imgBuf, lineLengthDwords);
    }
    //
    // Draw an inner rectangle, 'gap' pixels further in
    //
    for (uint32_t x = gap; x < width-gap; x++) {
        SetPx(x, gap, bpp, imgBuf, lineLengthDwords);
        SetPx(x, height-(1+gap), bpp, imgBuf, lineLengthDwords);
    }
    for (uint32_t y = gap; y < height-gap; y++) {
        SetPx(gap, y, bpp, imgBuf, lineLengthDwords);
        SetPx(width-(1+gap), y, bpp, imgBuf, lineLengthDwords);
    }

    return cmdBuf;
}

// ---------------------------------------------------------------------------- //
/**
    Commands which are sent to Meteor using PiSendCommand go through queues
    in PC memory followed by a queue in on-board PCC memory.  The bulk of the
    data in this queues is image data.

    When the PCC-E buffers are full, further commands are held in output
    buffers in the PrintEngine's PC memory.  When the output buffers are full,
    the PrintEngine's input queue starts to fill up.

    PiSendCommand will return RVAL_FULL when this input queue is full.

    In this situation the application should continually retry the command.
    
    When the PCC starts to print, the buffers will start to empty, giving
    room for further commands.

    A typical application should aim to keep queues as full as possible to
    minimise the risk of data underruns, so the RVAL_FULL condition is
    a good thing in most situations.
*/
eRET DoSendCommand(uint32_t* cmd) {
    eRET rVal;
    do {
        rVal = PiSendCommand(cmd);
    } while ( RVAL_FULL == rVal && !g_abortPrintJob );
    return rVal;
}

// ---------------------------------------------------------------------------- //
/**
    Sends the commands for a dummy print job.

    The command sequencing for a scanning print job is

    Start Job (PCMD_STARTJOB)
      For each scan ...
         Start Scan (PCMD_STARTSCAN)
         Image data for the scan (PCMD_IMAGE)
         End Scan (PCMD_ENDDOC)
    End Job (PCMD_ENDJOB)

    Each command is formed of a DWORD buffer.

    Offset 0 in the buffer is the command
    
    Offset 1 in the buffer is the count of following DWORDs 
    
    The remainder of the buffer is the command parameters, including the
    image data for PCMD_IMAGE.

    The commands are sent to Meteor using PiSendCommand
*/
void PrintJobThreadMain() {
    while ( !g_exitThreads ) {
        //
        // Abort sequencing; we don't send the PiAbort command to Meteor until
        // we have stopped sending print commands.
        //
        if ( g_abortPrintJob ) {
            g_abortPrintJob = false;
            g_sendAbortCmd = true;
        }

        //
        // Check for a start print job request every 100 milliseconds
        //
        std::this_thread::sleep_for(std::chrono::milliseconds(100));      
        if ( !g_startPrintJob ) {
            continue;
        }
        g_startPrintJob = false;

        while (!g_abortPrintJob) {

            //
            // The job ID is mainly for diagnostics.
            //
            // It allows a specific job to be found in a log file (e.g. an entry
            // such as "Rx:StartJob JobId=100 JobType=JT_SCAN Res=RES_HIGH Dwidth=0")
            // and it also forms part of the SimPrint filename
            //
            static uint32_t jobID = 100;

            uint32_t startJobCmd[] = {
                PCMD_STARTJOB,          // Command ID
                4,                      // Number of following DWORDs
                jobID++,                // Job ID
                JT_SCAN,                // This is a scanning job
                RES_HIGH,               // Print resolution; must be RES_HIGH for a scanning job
                0                       // Document width field is ignored for a scanning job
            };
            eRET rVal = DoSendCommand(startJobCmd);
            if (rVal != RVAL_OK) {
                //
                // If a previous print job still has data buffered, it's not possible to 
                // start a new one.  In this situation, sending the PCMD_STARTJOB command
                // returns RVAL_BUSY.
                //
                if (rVal == RVAL_BUSY) {
                    printf("--- Can't start new print job; the previous job is still busy ----\n");
                    if (KSoakTestModeeOff != g_soakTestState) {
                        // In soak test mode, sleep for a short time then try again: probably the overall
                        // PrintEngine state hasn't quite reached IDLE since the previous abort.
                        std::this_thread::sleep_for(std::chrono::milliseconds(10));
                        continue;
                    }
                } else {
                    printf("!!!! PCMD_STARTJOB failed: err=%d !!!!\n", rVal);
                }
                break;
            }
            printf("---- Started print job ---\n");

            //
            // Send a dummy print job with ten forwards scans
            //
            bool fwdScan = true;
            for (uint32_t i = 0; i < g_scanCount; i++) {
                //
                // Start scan, saying whether this is a forwards (encoder increasing) or
                // reverse (encoder decreasing) scan
                //
                uint32_t startScanCmd[] = {
                    PCMD_STARTSCAN,
                    1,
                    fwdScan ? (uint32_t)SD_FWD : (uint32_t)SD_REV
                };
                DoSendCommand(startScanCmd);

                //
                // Image data for the scan
                //
                uint32_t* imgCmd = CreateImageCommand(g_widthPx, g_heightPx, g_bitsPerPixel, i + 2);
                DoSendCommand(imgCmd);
                delete[] imgCmd;

                // 
                // End scan (document) command
                // 
                uint32_t endDocCmd[] = { PCMD_ENDDOC, 0 };
                DoSendCommand(endDocCmd);

                if (g_abortPrintJob) {
                    break;
                }

                printf("--- Sent scan %d ---\n", i + 1);
            }

            uint32_t endJobCmd[] = { PCMD_ENDJOB, 0 };
            DoSendCommand(endJobCmd);

            if (KSoakTestModeeOff == g_soakTestState) {
                break;
            }

            //
            // In soak test mode, wait for the two scans to print, then restart the job
            //
            while (!g_abortPrintJob) {
                const TAppStatus* appStatus = PiGetPrnStatus();
                if (KSoakTestModeComplete == g_soakTestState) {
                    break;
                }
            }

            g_soakTestState = KSoakTestModeStarting;
            PiAbort();
        }
    }
}

// ---------------------------------------------------------------------------- //
/**
    The application main loop kicks of three threads for Meteor interaction - 
    for print commands, control commands, and status - and then implements
    a basic console command processor which allows a test print job to be
    started, print heads to be powered on and off, and a force PD signal
    to be sent to kick off a simulated print.
*/
int DoAppMainLoop() {
    //
    // Initial status call, to check the global system setup.  This is the only place
    // we retrieve status in the main thread; subsequent status calls are in a dedicated
    // thread.  At this point we are the only thread calling into the PrintEngine,
    // so the API call should never fail.
    // 
    const TAppStatus* appStatus = PiGetPrnStatus();
    if ( nullptr == appStatus ) {
        eRET rVal = PiGetLastStatusError();
        printf("!!!! PiGetPrnStatus failed: err=%d !!!!\n", rVal);
        return rVal;
    }

    //
    // Check that the configuration file is set up with [System] Scanning = 1
    //
    // (This sample application can be easily modified to run with a 
    //  non-scanning system; just change the job type from JT_SCAN to
    //  JT_FIFO or JT_PRELOAD, and replace PCMD_STARTSCAN with
    //  PCMD_STARTFDOC or PCMD_STARTPDOC)
    //
    if ( !(appStatus->Control & BM_SCANNING) ) {
        printf("\n");
        printf("!!!! Configuration file must be set for scanning !!!!\n");
        printf("!!!! Set [System] Scanning = 1                   !!!!\n");
        printf("\n");
        return -1;
    }

    //
    // Note the bit depth for print data.  Here we assume this is fixed.
    //
    // (An application which needs to change the number of bits per pixel 
    //  can call PiSetParam with the CCP_BITS_PER_PIXEL parameter)
    //
    g_bitsPerPixel = appStatus->BitsPerPixel;

    // 
    // There are three threads in this sample application, for (1) print data, 
    // (2) control commands, and (3) status.
    // 
    std::thread printJobThread(PrintJobThreadMain);
    std::thread controlThread(ControlThreadMain);
    std::thread statusThread(StatusThreadMain);

    //
    // Initial output to the console of the supported commands
    //
    OutputCommandUsage();

    //
    // Command handler.  Depending on the console/shell settings, commands 
    // will normally need to be followed by an <enter> key press.
    //
    while ( !g_exitThreads ) {
        int cmd = getchar();
        switch (cmd) {
        case CMD_QUIT:          // -- Quit application
            g_exitThreads = true;
            break;
        case CMD_HP_ON:         // --- Head power on
        case CMD_HP_OFF:        // --- Head power off
            if ( g_headPowerBusy ) {
                printf("!!!! Head power is busy !!!!\n");
                break;
            }
            // --- Fall through to sending the command ---
        case CMD_FORCE_PD:      // ---- Force product detect
            if ( g_controlCommand == -1 ) {
                g_controlCommand = cmd;
            } else {
                printf("!!!! Control thread busy !!!!\n");
            }
            break;
        case CMD_ABORT:         // ---- Abort
            g_abortPrintJob = true;
            break;
        case CMD_START_JOB:     // ---- Start print job
            g_startPrintJob = true; 
            break;
        default:                // ---- Command not recognised
            OutputCommandUsage();
            break;
        }
        while ( getchar() != '\n' ) {}
    }

    //
    // The threads will all exit once they see that the g_exitThreads flag
    // is set.  Wait for this to happen before exiting the application.
    //
    printJobThread.join();
    controlThread.join();
    statusThread.join();

    return 0;
}

// ---------------------------------------------------------------------------- //
/**
    Parse the application's command line arguments, using them to set up the
    global variables for the configuration file path etc.

    @return true if the arguments are valid, false otherwise
*/
static bool ParseArgs(int argc, char** argv) {
    bool argsValid = true;
    for (int i = 1; i < argc; i++) {
        const char* arg = argv[i];
        if (strncmp(arg, "-c", 2) == 0) {
            g_cfgFile = arg + 2;
        } else if (strncmp(arg, "-t", 2) == 0) {
            g_soakTestState = KSoakTestModeStarting;
            g_scanCount = 2;
        } else if (strncmp(arg, "-s", 2) == 0) {
            g_scanCount = atoi(arg+2);
        } else if (strncmp(arg, "-w", 2) == 0) {
            g_widthPx = atoi(arg + 2);
        } else if (strncmp(arg, "-h", 2) == 0) {
            g_heightPx = atoi(arg + 2);
        } else {
            argsValid = false;
        }
    }

    if (!argsValid || 0 == g_scanCount || 0 == g_widthPx || 0 == g_heightPx) {
        printf("\n");
        printf("\n");
        printf("---- SampleScanPrint usage                                          ----\n");
        printf("---- N.B. no whitespace between the option and the parameter value  ----\n");
        printf("----                                                                ----\n");
        printf("----    -c<FILENAME.CFG>        Config file path                    ----\n");
        printf("----    -s<SCAN_COUNT>          Number of swaths (default 10)       ----\n");
        printf("----    -w<WIDTH_PX>            Image width in pixels (default 500) ----\n");
        printf("----    -h<HEIGHT_PC>           Image height in pixels (default 500)----\n");
        printf("----    -t                      Enable soak test mode (default off) ----\n");
        printf("\n");
        return false;
    }

    return true;
}


// ---------------------------------------------------------------------------- //
/**
    Application entry point
*/
int main(int argc, char** argv) {
    //
    // Parse the command line options and display the available parameters if
    // there's an error.  ? or -? can be used to display the options.
    //
    if (!ParseArgs(argc, argv)) {
        return -1;
    }

#ifdef _WIN32
    //
    // On Windows, find the installed location of the Meteor runtime.
    // This must be done before the first call into the PrinterInterface, which is
    // linked as a "delay loaded" DLL.
    //
    extern bool AddMeteorPath(bool abAdd32BitRuntime);
    if (!AddMeteorPath(sizeof(void*) == sizeof(uint32))) {
        return -1;
    }
#endif

    // Start the Meteor PrintEngine using the configuration file path provided on
    // the command line.  On Windows, null can be used to load the most recently
    // used configuration file (as set in the register).
    // There's no real need to validate the path here; PiStartPrintEngine will
    // fail if the file isn't found.
    //
    eRET rVal = PiStartPrintEngine(g_cfgFile);
    printf("\n");
    printf("---- PiStartPrintEngine returned %d ---- \n", rVal);
    if ( RVAL_OK != rVal ) {
        printf("!!!! PiStartPrintEngine failed: err=%d !!!!\n", rVal);
        return rVal;
    }
    
    //
    // Connect to the Meteor PrinterInterface
    //
    rVal = PiOpenPrinter();
    printf("---- PiOpenPrinter returned %d ----\n", rVal);
    if (RVAL_OK != rVal) {
        printf("!!!! PiOpenPrinter failed: err=%d !!!!\n", rVal);
        PiStopPrintEngine(1);
        return rVal;
    }

    //
    // Now Meteor is running, run the application's main loop.  This accepts
    // a few basic single character commands from the console / shell, to
    // start a print job, abort a print job, force a product detect,
    // and enable/disable head power
    //
    int exitCode = DoAppMainLoop();

    //
    // Make sure we've not tried to exit mid-print job
    //
    PiAbort();
    
    //
    // Wait until the PrintEngine is ready to be closed; e.g. this 
    // could take a second or two if the PrintEngine is having to 
    // abort a print job
    // 
    while ( !PiCanClosePrinter() ) {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }

    //
    // Close the PrinterInterface connection
    //
    PiClosePrinter();
    
    //
    // Stop the Meteor PrintEngine
    //
    PiStopPrintEngine(0);

    return exitCode;
}
