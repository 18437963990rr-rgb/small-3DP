/**
    Copyright (c) 2023, Meteor Inkjet Limited

    Sample code demonstrating how to set the path to the Meteor binaries at runtime.

    Best practice is for there only ever to be a single installation of the Meteor SDK on a PC.
    The Meteor dll files should **never** be copied from the installation location.
    This is to avoid potential problems where the application can end up using an out of date version 
    of the PrintEngine.dll by mistake.
    
    The location of the binaries can be found at runtime using AddMeteorPath.

    PrinterInterface.dll should be added to the project's delay loaded DLLS in 
    "Configuration Properties | Linker | Input | Delay Loaded Dlls".
    This allows the application to be linked statically, but the PrinterInterface.dll is 
    not loaded into the process until the first call is made into it.  
    AddMeteorPath should be called before this point to set up the runtime directory.

    [ A simpler alternative is to include the Meteor runtime directory in the system PATH
      (e.g. PATH=%PATH%;"C:\Program Files\Meteor Inkjet\Meteor\Api\amd64").
      However, depending on the requirements for system set up, this is not always suitable. ]
*/
#include <windows.h>
#include <stdio.h>

// ----------------------------------------------------------------------------------------------------------------------------------------------- //
/**
    Looks in the registry to find the install location of the Meteor PrintEngine / PrinterInterface,
    and sets this as the DLL load directory (SetDllDirectory).

    @param abAdd32BitRuntime        true:   add the 32 bit runtime to SetDllDirectory (available in both a 32 bit and 64 bit install)
                                    false:  add the 64 bit runtime to SetDllDirectory (only available in a 64 bit install)
*/
bool AddMeteorPath(bool abAdd32BitRuntime) {
    
    //
    // There are two registry keys where the PrintEngine SDK installer stores the install path:
    // - 'HKEY_CURRENT_USER\Software\Meteor Inkjet\Install 64 bit' for a 64 bit install
    // - 'HKEY_CURRENT_USER\Software\Meteor Inkjet\Install 32 bit' for a 32 bit install
    // Note that a 64 bit install includes both 64 bit and 32 bit runtime components.
    // Here we are assuming that the installation is 64 bit.
    //
    const char* meteorRegKey = "Software\\Meteor Inkjet\\Install 64 bit";
    HKEY meteorInstallKey;
    LSTATUS result = RegOpenKeyExA(
        HKEY_CURRENT_USER, 
        meteorRegKey,
        0, 
        KEY_ENUMERATE_SUB_KEYS | KEY_QUERY_VALUE, 
        &meteorInstallKey);

    if (ERROR_SUCCESS != result) {
        printf("*** Failed to open registry key '%s' result=%d\n", meteorRegKey, result);
        return false;
    }

    // In some rare situations it is possible for multiple instances of the PrintEngine SDK to be installed.
    // Here we look for the most recent version.
    __int64 maxCombinedVersion = 0;
    char meteorApiPath[MAX_PATH] = { 0 };

    if (ERROR_SUCCESS == result) {
        //
        // Enumerate the subkeys of "HKEY_CURRENT_USER\Software\Meteor Inkjet\Install 64 bit" for each installed version.
        // Normally there should only ever be one.
        //
        int subKeyIdx = 0;
        for (;;) {
            const int KMaxNameLen = 64;
            char subKeyName[KMaxNameLen];
            DWORD nameLen = KMaxNameLen;
            result = RegEnumKeyExA(meteorInstallKey, subKeyIdx, subKeyName, &nameLen, NULL, NULL, NULL, NULL);
            if (ERROR_NO_MORE_ITEMS == result) {
                break;
            }
            if (ERROR_SUCCESS != result) {
                break;
            }

            // The subkey name is the version number, formatted MAJOR.MINOR.BUILD.REVISION
            int vMajor, vMinor, vBuild, vRevision;
            if (4 != sscanf_s(subKeyName, "%d.%d.%d.%d", &vMajor, &vMinor, &vBuild, &vRevision)) {
                continue;
            }
            // A combination of "BUILD" and "REVISION" uniquely identifies a release
            const __int64 combinedVersion = ((__int64)vBuild << 32) | vRevision;
            if (combinedVersion < maxCombinedVersion) {
                continue;
            }
            // Finally get the installation path for this version
            DWORD dataLen = sizeof(meteorApiPath);
            result = RegGetValueA(meteorInstallKey, subKeyName, "InstallPath", RRF_RT_REG_SZ, NULL, meteorApiPath, &dataLen);
            if (ERROR_SUCCESS == result) {
                maxCombinedVersion = combinedVersion;
            }
            subKeyIdx++;
        }
    }

    if (-1 != maxCombinedVersion) {
        if (!abAdd32BitRuntime) {
            // Path to the 64 bit API runtime components.  
            strcat_s(meteorApiPath, "Api\\amd64");
        } else {
            // Path to the 64 bit API runtime components.  
            strcat_s(meteorApiPath, "Api\\x86");
        }

        printf("=== Loading Meteor runtime from '%s\n", meteorApiPath);

        // If nothing else is using SetDllDirectory within the processes, we can just set it and leave it.
        // PrinterInterface.dll will be loaded by the delay load mechanism when we first call into the library.
        // Otherwise, we can load the DLL immediately then reset SetDllDirectory.
        // PrinterInterface.dll may or may not need to be unloaded later, depending on the application requirements
        SetDllDirectoryA(meteorApiPath);
        

        // HMODULE hPrinterInterface = LoadLibraryA("PrinterInterface.dll");
        // SetDllDirectoryA(NULL);

        return true;
    }

    printf("*** Failed to find Meteor install !!\n");

    RegCloseKey(meteorInstallKey);

    return true;
}
