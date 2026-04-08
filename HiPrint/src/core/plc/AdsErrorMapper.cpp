// AdsErrorMapper.cpp

#include "AdsErrorMapper.h"

//静态成员变量定义
QHash<int, QString>AdsErrorMapper::m_errorMap;

AdsErrorMapper::AdsErrorMapper() {

  
}

// 获取错误描述（int 类型参数）
QString AdsErrorMapper::getErrorDescription(int errorCode){

    if (m_errorMap.isEmpty()) {
        initializeErrorDescriptions();
    }
    if (m_errorMap.contains(errorCode)) {
        return m_errorMap.value(errorCode);
    }
    return "unknown Error";  // 如果找不到错误码，返回默认错误描述
}

void AdsErrorMapper::initializeErrorDescriptions()
{
    m_errorMap[0] = "no error";
    m_errorMap[1] = "Internal error";
    m_errorMap[2] = "No Rtime";
    m_errorMap[3] = "Allocation locked memory error";
    m_errorMap[4] = "Insert mailbox error";
    m_errorMap[5] = "Wrong receive HMSG";
    m_errorMap[6] = "Target port not found";
    m_errorMap[7] = "Target machine not found";
    m_errorMap[8] = "Unknown command ID";
    m_errorMap[9] = "Bad task ID";
    m_errorMap[10] = "No IO";
    m_errorMap[11] = "Unknown AMS command";
    m_errorMap[12] = "Win32 error";
    m_errorMap[13] = "Port not connected";
    m_errorMap[14] = "Invalid AMS length";
    m_errorMap[15] = "Invalid AMS Net ID";
    m_errorMap[16] = "Low Installation level";
    m_errorMap[17] = "No debug available";
    m_errorMap[18] = "Port disabled";
    m_errorMap[19] = "Port already connected";
    m_errorMap[20] = "AMS Sync Win32 error";
    m_errorMap[21] = "AMS Sync Timeout";
    m_errorMap[22] = "AMS Sync AMS error";
    m_errorMap[23] = "AMS Sync no index map";
    m_errorMap[24] = "Invalid AMS port";
    m_errorMap[25] = "No memory";
    m_errorMap[26] = "TCP send error";
    m_errorMap[27] = "Host unreachable";
    m_errorMap[1280] = "Router: no locked memory";
    m_errorMap[1282] = "Router: mailbox full";
    m_errorMap[1792] = "Error class <device error>";
    m_errorMap[1793] = "Service is not supported by server";
    m_errorMap[1794] = "Invalid index group";
    m_errorMap[1795] = "Invalid index offset";
    m_errorMap[1796] = "Reading/writing not permitted";
    m_errorMap[1797] = "Parameter size not correct";
    m_errorMap[1798] = "Invalid parameter value(s)";
    m_errorMap[1799] = "Device is not in a ready state";
    m_errorMap[1800] = "Device is busy";
    m_errorMap[1801] = "Invalid context (must be in Windows)";
    m_errorMap[1802] = "Out of memory";
    m_errorMap[1803] = "Invalid parameter value(s)";
    m_errorMap[1804] = "Not found (files, ...)";
    m_errorMap[1805] = "Syntax error in command or file";
    m_errorMap[1806] = "Objects do not match";
    m_errorMap[1807] = "Object already exists";
    m_errorMap[1808] = "Symbol not found";
    m_errorMap[1809] = "Symbol version invalid";
    m_errorMap[1810] = "Server is in invalid state";
    m_errorMap[1811] = "AdsTransMode not supported";
    m_errorMap[1812] = "Notification handle is invalid";
    m_errorMap[1813] = "Notification client not registered";
    m_errorMap[1814] = "No more notification handles";
    m_errorMap[1815] = "Size for watch too big";
    m_errorMap[1816] = "Device not initialized";
    m_errorMap[1817] = "Device has a timeout";
    m_errorMap[1818] = "Query interface failed";
    m_errorMap[1819] = "Wrong interface required";
    m_errorMap[1820] = "Class ID is invalid";
    m_errorMap[1821] = "Object ID is invalid";
    m_errorMap[1822] = "Request is pending";
    m_errorMap[1823] = "Request is aborted";
    m_errorMap[1824] = "Signal warning";
    m_errorMap[1825] = "Invalid array index";
    m_errorMap[1856] = "Error class <client error>";
    m_errorMap[1857] = "Invalid parameter at service";
    m_errorMap[1858] = "Polling list is empty";
    m_errorMap[1859] = "Var connection already in use";
    m_errorMap[1860] = "Invoke ID in use";
    m_errorMap[1861] = "Timeout elapsed";
    m_errorMap[1862] = "Error in Win32 subsystem";
    m_errorMap[1864] = "Ads-port not opened";
    m_errorMap[1872] = "Internal error in ads sync";
    m_errorMap[1873] = "Hash table overflow";
    m_errorMap[1874] = "Key not found in hash";
    m_errorMap[1875] = "No more symbols in cache";
}

