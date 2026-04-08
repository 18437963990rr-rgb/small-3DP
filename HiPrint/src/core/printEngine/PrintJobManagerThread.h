#ifndef PRINTJOBTHREAD_H
#define PRINTJOBTHREAD_H

#include <QThread>
#include <QMutex>
#include <QWaitCondition>
#include <QImage>
#include <QEventLoop>
#include "../../common/utils/FileOp.h"
#include "../../core/imageProcessing/ImageProcessorFactory.h"
#include "../../core/printEngine/MeteorApi.h"
#include "../../core/printEngine/MeteorErrorMap.h"
#include "../../core/plc/BeckhoffPlc.h"
#include "../../core/plc/AdsErrorMapper.h"
#include "../../common/config/PrintJobTypeDef.h"
#include "../../common/global/ParameterManager.h"


enum class PRINT_JOBSTATUS {
    PRINT_STOPPED = 0,  // 线程停止
    PRINT_RUNNING = 1,  // 线程运行
    PRINT_PAUSED = 2   // 线程暂停
};

class PrintJobManagerThread : public QThread {

    Q_OBJECT

public:
    explicit PrintJobManagerThread(QObject* parent = nullptr);
    ~PrintJobManagerThread();
public:
    void startPrint(PrintJobParam printJobParam);// 开始打印
    void pauseOrResumePrint();// 暂停/继续切换
    void stopPrint();// 停止打印

    PRINT_JOBSTATUS getPrintJobStatus() const;// 获取当前打印状态

protected:
    void run() override;  // 线程入口

private:
    PRINT_JOBSTATUS printJobStatus;
    PrintJobParam printThreadJobParam;
    std::unique_ptr<FileOp> fileOp;            
    std::unique_ptr<MeteorApi> meteorApi;
    BeckhoffPlc* m_beckhoffPlc;
    QStringList fileList;
    QString printFileName;
    QMutex mutex;
    QWaitCondition condition;
    bool bExitWorkThread;  // 是否退出线程
    bool bPaused;  // 是否处于暂停状态
  
public:
    void executePrintJob(const PrintJobParam &printJobParam);//执行打印工作任务
    bool printSingleImage(int imageIndex,QString &printFileName, unsigned int& jobID, std::vector<int>& printPassIndex,
        PrintImageParam& printImageParam, int waitTimeOut, const PrintJobParam& printJobParam);
    void setBeckhoffPlc(BeckhoffPlc* beckhoffPlc) { m_beckhoffPlc = beckhoffPlc; }

       
signals:
    void printStarted(); // 开始打印信号
    void printFinished();// 打印完成信号
    void imagePrinted(int index);// 每张图片打印完成信号
    void printStatus(int row, PRINT_STATUS printStatus);//发送打印状态
    void sendPrintImageParam(const QString& imagePath, const PrintImageParam& printImageParam);//更新打印图片
    void printProgressUpdated(int printedFiles, int totalFiles,QString currentfileName, PrintImageParam& printImageParam); // 打印进度更新信号
};

#endif // PRINTJOBTHREAD_H

