#include "PrintJobManagerThread.h"
#include <QElapsedTimer>
#include <QMutexLocker>
#include <QDebug>

PrintJobManagerThread::PrintJobManagerThread(QObject* parent)
    : QThread(parent)
    , bExitWorkThread(false)
    , bPaused(false)
    , printJobStatus(PRINT_JOBSTATUS::PRINT_STOPPED)
    , meteorApi(std::make_unique<MeteorApi>())
    , m_beckhoffPlc(nullptr)
    , fileOp(std::make_unique<FileOp>())
{
    
}

PrintJobManagerThread::~PrintJobManagerThread() {
    stopPrint();
}

void PrintJobManagerThread::startPrint(PrintJobParam printJobParam) {
    if (isRunning()) {
        return;
    }  
    printThreadJobParam = printJobParam;
    bExitWorkThread = false;
    bPaused = false;
    printJobStatus = PRINT_JOBSTATUS::PRINT_RUNNING;
    start();  // 启动线程
}

void PrintJobManagerThread::run() {

    executePrintJob(printThreadJobParam); //执行打印工作
}

void PrintJobManagerThread::pauseOrResumePrint() {
    QMutexLocker locker(&mutex);
    if (bPaused) {
        printJobStatus = PRINT_JOBSTATUS::PRINT_RUNNING;
        bPaused = false;
        condition.wakeOne();  //唤醒线程    
    }
    else {
        printJobStatus = PRINT_JOBSTATUS::PRINT_PAUSED;
        bPaused = true;
    }
}

void PrintJobManagerThread::stopPrint() {
    QMutexLocker locker(&mutex);
    bExitWorkThread = true;
    printJobStatus = PRINT_JOBSTATUS::PRINT_STOPPED;
    condition.wakeOne();  // 解除等待状态
}

PRINT_JOBSTATUS PrintJobManagerThread::getPrintJobStatus() const {
    return printJobStatus;
}

void PrintJobManagerThread::executePrintJob(const PrintJobParam& printJobParam) {

    std::vector<int> printPassIndex;//打印区域序号
    PrintImageParam printImageParam;//打印图片参数
    static unsigned int jobID = 0;//工作编号ID
    int waitTimeOut = 1000 * 3; //忙碌超时时间
   

    fileList = printJobParam.printImageList;
    if (fileList.isEmpty()) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
            "Print Image Files is Empty!!!"); 
    }

    //连续打印
    if (printJobParam.modeType == PrintModeType::CONTINUOUS) {
        int currentPrintImageIndex = 0;
        while (!bExitWorkThread && currentPrintImageIndex < fileList.size()) {
            if (!printSingleImage(currentPrintImageIndex, printFileName, jobID, printPassIndex, printImageParam, waitTimeOut, printJobParam)) {
                break;
            }
            currentPrintImageIndex++;
            emit printProgressUpdated(currentPrintImageIndex, fileList.size(),printFileName, printImageParam);
        }
    }//单文件打印
    else if (printJobParam.modeType == PrintModeType::SINGLEFILE) {
        int currentSigleLayerPrintTimes = 0;
        QString targetName = QString::fromStdString(printJobParam.singleFileName);
        int idx = fileList.indexOf(targetName);
        if (idx == -1) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, QString("指定文件 %1 不存在").arg(targetName).toStdString());
            return;
        }
        for (int i = 0; i < printJobParam.singleFilePrintCount && !bExitWorkThread; ++i) {
            if (printSingleImage(idx, printFileName,jobID, printPassIndex, printImageParam, waitTimeOut, printJobParam)) {
                currentSigleLayerPrintTimes++;
                emit printProgressUpdated(currentSigleLayerPrintTimes, printJobParam.singleFilePrintCount,printFileName, printImageParam);
            } else {
                break;
            }  
        }   
    }
}

bool PrintJobManagerThread::printSingleImage(int imageIndex, QString& printFileName,unsigned int& jobID, 
    std::vector<int>& printPassIndex, PrintImageParam& printImageParam, int waitTimeOut, const PrintJobParam& printJobParam)
{

    bool scanFinishFlag = false;
    unsigned int scanTimes = 0;

    //停止打印
    if (printJobStatus == PRINT_JOBSTATUS::PRINT_STOPPED) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, "Print Task is Stop!!!");
        return false;
    }
    //暂停打印
    if (printJobStatus == PRINT_JOBSTATUS::PRINT_PAUSED) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, "Print Task is Pause!!!");
        QMutexLocker locker(&mutex);
        condition.wait(&mutex);
    }
    //进行打印
    if (printJobStatus == PRINT_JOBSTATUS::PRINT_RUNNING) {
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
            QString("Start Print Job,Current Print Image Name is %1")
            .arg(QFileInfo(fileList.at(imageIndex)).fileName()).toStdString());
        int ret = meteorApi->sendAbortCommand();
        if (ret != RVAL_OK) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, QString("Send Abort Command To Meteror Fail!,%1")
                .arg(MeteorErrorMap::getErrorDescription(ret)).toStdString());
            return false;
        }
     
        //加载打印图片
        auto imageProcessor = ImageProcessorFactory::create(fileList.at(imageIndex));
        bool success = imageProcessor->loadImage(fileList.at(imageIndex));
        if (!success) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
                QString("Load 1 Bit Tiff Image Fail!!!,Current Print Image Name is %1")
                .arg(QFileInfo(fileList.at(imageIndex)).fileName()).toStdString());
            return false;
        }
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
            QString("Load 1 Bit Tiff Image Success!,Current Print Image Name is %1")
            .arg(QFileInfo(fileList.at(imageIndex)).fileName()).toStdString());

        printImageParam.printImage = imageProcessor->convertToQImage();
        printImageParam.width = imageProcessor->getWidth();
        printImageParam.height = imageProcessor->getHeight();
        printImageParam.xDimension = imageProcessor->getXDimension();
        printImageParam.yDimension = imageProcessor->getYDimension();
        printImageParam.xResolution = imageProcessor->getXResolution();
        printImageParam.yResolution = imageProcessor->getYResolution();
        printImageParam.bitsPerSample = imageProcessor->getBitsPerSample();

        printFileName = fileList.at(imageIndex);
        emit sendPrintImageParam(fileList.at(imageIndex), printImageParam);

         ret = meteorApi->setHome();
         if (ret != RVAL_OK) {
             LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
                 QString("Set Home Fail,Value is %1").arg(MeteorErrorMap::getErrorDescription(ret)).toStdString());
             return false;

         }
       
        double reverseOffsetMm = ParameterManager::instance().getReverseOffset();
        double bidiXAdjustValue = (reverseOffsetMm / 25.4) * printImageParam.xResolution * 100;
        int32_t bidiXAdjustSigned = qRound(bidiXAdjustValue);
        uint32_t bidiXAdjustUnsigned = static_cast<uint32_t>(bidiXAdjustSigned);
        ret = meteorApi->setPrinterParameter(CCP_BIDI_XADJUST, bidiXAdjustUnsigned);
        if (ret != RVAL_OK) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
                QString("Set CCP_BIDI_XADJUST Command To Meteror Fail! %1 (value: %2)").arg(MeteorErrorMap::getErrorDescription(ret)).arg(bidiXAdjustSigned).toStdString());
            return false;
        }

        // 打印作业开始
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
            QString("Start Print Job,Current Print Image Name is %1")
            .arg(QFileInfo(fileList.at(imageIndex)).fileName()).toStdString());
        uint32_t startJobCmd[] = {
            PCMD_STARTJOB,
            4,
            jobID++,
            JT_SCAN,
            RES_HIGH,
            static_cast<uint32_t>(imageProcessor->getWidth())
        };
        int nRet = meteorApi->sendCommand(startJobCmd, 10, 1000);
        if (nRet != RVAL_OK) {
            if (nRet == RVAL_BUSY) {
                LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
                    "--- Can't start new print job; the previous job is still busy");
            }
            else {
                LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
                    "Send PCMD_STARTJOB Command Failed");
            }
            return false;
        }

        //通过图像翻译器发送图像数据
        std::vector<unsigned int*> imageCmmand = imageProcessor->createImageCommand(
            1024 * printJobParam.printHeadNum, scanTimes, printJobParam.yTop, printJobParam.trueBpp,
            printJobParam.plane, printJobParam.xLeft, printPassIndex, !printJobParam.bAutoSkipWhite);

        for (uint32_t i = 0; i < scanTimes; i++) {
            uint32_t startScanCmd[] = {
                PCMD_STARTSCAN,
                1,
                (i & 1) == 0 ? (uint32_t)SD_FWD : (uint32_t)SD_REV
                //(uint32_t)SD_FWD
            };
            int ret = meteorApi->sendCommand(startScanCmd);
            if (ret != RVAL_OK) {
                LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
                    QString("Send Scan Cmd To Metor Fail!,%1,Current Scan Index is %2")
                    .arg(MeteorErrorMap::getErrorDescription(ret)).arg(i).toStdString());
            }
            else {
                LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
                    QString("Send Scan Cmd To Metor Success!,%1,Current Scan Index is %2")
                    .arg(MeteorErrorMap::getErrorDescription(ret)).arg(i).toStdString());
            }
            ret = meteorApi->sendCommand(imageCmmand[i]);
            if (ret != RVAL_OK) {
                LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
                    QString("Send Image Data Command To Metor Fail!,%1")
                    .arg(MeteorErrorMap::getErrorDescription(ret)).toStdString());
            }
            else {
                LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
                    "Send Image Data Command To Metor Success!");
            }
            uint32_t endDocCmd[] = { PCMD_ENDDOC, 0 };
            ret = meteorApi->sendCommand(endDocCmd);
            if (ret != RVAL_OK) {
                LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
                    QString("Send End Doc Cmd To Meteor Fail!,%1,Current End Doc Cmd Index is %2")
                    .arg(MeteorErrorMap::getErrorDescription(ret)).arg(i).toStdString());
            }
            else {
                LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
                    QString("Send End Doc Cmd To Meteor Success!,%1,Current End Doc Cmd Index is %2")
                    .arg(MeteorErrorMap::getErrorDescription(ret)).arg(i).toStdString());
            }
        }
        //打印作业结束
        uint32_t endJobCmd[] = { PCMD_ENDJOB, 0 };
        ret = meteorApi->sendCommand(endJobCmd);
        if (ret != RVAL_OK) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
                QString("Send End Job Command Fail!,%1")
                .arg(MeteorErrorMap::getErrorDescription(ret)).toStdString());
        }
        else {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, "Send End Job Command Success!");
        }

        //跳步打印
      /*  ret = m_beckhoffPlc->sendPrintPassPosition(printJobParam.imagePrintPass, printPassIndex.data(), printPassIndex.size());
        if (ret != 0) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, QString("Send PrintPosition Fail!, %1")
                .arg(AdsErrorMapper::getErrorDescription(ret)).toStdString());
        }
        else {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, "Send PrintPosition Command Success!");
        }*/

        ret = m_beckhoffPlc->sendScanTimes(printJobParam.scanTimesCommand, scanTimes);
        if (ret != 0) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, QString("Send ScanCommand Fail!, %1")
                .arg(AdsErrorMapper::getErrorDescription(ret)).toStdString());
        }
        else {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, "Send ScanCommand Command Success!");
        }

        ret = m_beckhoffPlc->sendScanCommand(printJobParam.printMode, TRUE);
        if (ret != 0) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, QString("Do Scan Command Fail!, %1")
                .arg(AdsErrorMapper::getErrorDescription(ret)).toStdString());
        }
        else {

            //单层打印
            if (printJobParam.singleFilePrintCount > 1 || printJobParam.modeType == PrintModeType::CONTINUOUS) {
                ret = m_beckhoffPlc->sendScanCommand(printJobParam.autoPrintCommand, TRUE);
                if (ret != 0) {
                    LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, QString("Do Auto Scan Command Fail!, %1")
                        .arg(AdsErrorMapper::getErrorDescription(ret)).toStdString());
                }
            }
            QThread::msleep(100);
            ret = m_beckhoffPlc->sendScanCommand(printJobParam.printMode, FALSE);
            LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, "Do Scan Command Success!");
            emit printStatus(imageIndex, PRINT_STATUS::PRINTING);
        }
        ret = m_beckhoffPlc->sendScanCommand(printJobParam.scanFinishCommand, FALSE);
        if (ret != 0) {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR, QString("Do Scan Command Fail!, %1")
                .arg(AdsErrorMapper::getErrorDescription(ret)).toStdString());
        }
        else {
            LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, "Do Scan Finished Reset Command Success!");
        }
        QElapsedTimer timer;
        timer.start();
        while (true) {
            long ret = m_beckhoffPlc->receiveScanCommand(printJobParam.scanFinishCommand, scanFinishFlag);
            if (ret != 0) {
                LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
                    QString("Receive Scan Finished Command Fail!,%1")
                    .arg(AdsErrorMapper::getErrorDescription(ret)).toStdString());
            }
            else {
                if (scanFinishFlag) {
                    LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO, "Receive Scan Finished Command Success!");
                    break;
                }
            }
            QThread::msleep(10);
            if (timer.elapsed() > printJobParam.maxWaitTimeMs) {
                LogManager::instance().logMessage(LOG_LEVEL::LVL_ERROR,
                    "Motion timeout, Stop moving to next image!!!");
                break;
            }
        }
        emit printStatus(imageIndex, PRINT_STATUS::PRINTING_FINISHED);
        LogManager::instance().logMessage(LOG_LEVEL::LVL_INFO,
            QString("Print [%1] Layer Finished!").arg(imageIndex).toStdString());
        return true;
    }
}









