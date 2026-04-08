#include "MeteorErrorMap.h"

// 静态成员变量的定义
QHash<int, QString> MeteorErrorMap::errorDescriptions;

// 构造函数：初始化错误码与描述的映射
MeteorErrorMap::MeteorErrorMap() {
   
}

// 根据错误码获取描述
QString MeteorErrorMap::getErrorDescription(int code){

    if (errorDescriptions.isEmpty()) {
        initializeErrorDescriptions();
    }
    if (errorDescriptions.contains(code)) {
        return errorDescriptions[code];
    }
    else {
        return "unknown Error Code";  // 返回未知错误信息
    }
}

void MeteorErrorMap::initializeErrorDescriptions()
{
    errorDescriptions = {
       {static_cast<int>(METEOR_ERRORCODE::RVAL_OK), "No errors"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_FAULT), "General error"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_EOF), "End of file"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_NOFILE), "File not found"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_BADPARAM), "Invalid parameter(s) encountered"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_BADSEQ), "Command called out of sequence."},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_EXISTS), "Printer Interface already open in the calling process."},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_NOTOPEN), "Printer Interface has not been opened."},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_SSFULL), "SSFULL error"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_FULL), "Memory buffers are full. Retry command later."},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_EMPTY), "Buffer is empty. No more messages available."},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_NOMEM), "Out of memory"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_VERSION), "Version mismatch. PiOpenPrinter returns RVAL_VERSION if the Printer Interface and the Print Engine have different build numbers."},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_MISMATCH), "Mismatch error"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_BUSY), "Still executing previous command. Retry command later."},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_CLOSING), "Closing error"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_NOTUSED), "Not used"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_NO_DRIVER), "No driver found"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_NO_PRINTER), "Print Engine is not running"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_BAD_LICENSE), "License key specified was invalid"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_CLAIMED), "Printer Interface already open in another process"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_BADTIFFFORMAT), "Unsupported TIFF format"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_BADBITSPERPIXEL), "Incorrect bits per pixel"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_ENGINE_RUNNING), "Print engine is already running in another process"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_NOT_AVAILABLE), "Command is not available in the current configuration"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_NOT_HOSTING), "Command is only available to an application which is hosting the Meteor PrintEngine"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_UNKNOWN_TYPE), "Unknown type (e.g. of file)"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_BAD_TYPE), "Incorrect type (e.g. of file)"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_STRUCT_SIZE_MISMATCH), "A structure size is incorrect."},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_MEM_LIMIT), "The memory limit for image buffer allocation has been reached"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_REINIT), "The command cannot be sent because the Meteor PrintEngine is (re-)initialising"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_CMD_BUF_MAPPING_FAILED), "The command buffer cannot be mapped into process memory"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_EXCEPTION), "An exception occurred when attempting to start the PrintEngine."},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_BADPATH), "A file path is incorrectly formatted"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_CMD_TOO_BIG), "The command is too big to fit in the PrinterInterface -> PrintEngine buffer."},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_BLOCKING), "The PrinterInterface command queue is already in a blocking command in another thread"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_ABORTED), "A blocked command was aborted"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_PARAMID_NOT_REGISTERED), "An attempt to set an eCFGPARAMEx in a scope where the current head type doesn't use the parameter"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_FAILED_TO_CREATE_PROCESS), "Failed to create a sub-process (e.g. for status report creation)"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_TIMEOUT), "An operation timed out"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_BADSIZE), "Incorrect size (e.g. the size of a file on disk does not match with its file header details)"},
       {static_cast<int>(METEOR_ERRORCODE::RVAL_INPUT_EXECPTION), "A fatal PrintEngine exception occurred during input command processing."}
    };
}
