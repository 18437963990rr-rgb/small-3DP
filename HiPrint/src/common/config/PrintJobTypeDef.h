#ifndef PRINTJOB_TYPE_H
#define PRINTJOB_TYPE_H

//打印状态
enum class PRINT_STATUS {
	NO_PRINTING = 0,//未打印
	PRINTING = 1,//打印中
	PAUSE_PRINTING = 2,//暂停打印
    PRINTING_FINISHED = 3,//打印完成
	WAIT_TO_PRINTING = 4 //等待打印
};

//打印工作类型
enum class PrintModeType {
    CONTINUOUS,  //连续
    SINGLEFILE  //单文件打印
};


//打印工作命令参数
struct PrintJobParam {
    unsigned int yTop;//从文档顶部的Y偏移
    unsigned int trueBpp;//图片位数
    unsigned int plane;//通道数
    unsigned int xLeft;//图像与文档左边缘的Y偏移
    unsigned int printHeadNum;//打印头的数量
    unsigned int maxWaitTimeMs;//超时等待时间
    std::string printMode; //打印模式 
    std::string scanFinishCommand;//扫描完成命令
    std::string imagePrintPass;//图像打印区域
    std::string scanTimesCommand;//扫描打印次数
    std::string printDirPath;//打印目录
    bool bAutoSkipWhite;//是否自动跳白
    PrintModeType modeType = PrintModeType::CONTINUOUS;//工作模式
    std::string singleFileName;//单文件名
    int singleFilePrintCount = 1;//打印次数
    QStringList printImageList;//打印图像列表
    std::string autoPrintCommand;//自动打印命令
   
};

//打印图像参数
struct PrintImageParam {
    QImage printImage; //打印图片
    int width;//图片宽度
    int height;//图片高度
    int xDimension;//x方向尺寸
    int yDimension;//y方向尺寸
    int xResolution;//x方向分辨率
    int yResolution;//y方向分辨率
    int bitsPerSample;//位数

};


#endif //PRINTJOB_TYPE_H
