using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _3DP_KeyLibrary
{
    public class 组件接口集合
    {
    }

    public interface IParameter
    {
        /// <summary>
        /// @param 成型阶段层参数
        /// </summary>
        void SetLayerPara(Parameters 成型阶段层参数);
        /// <summary>
        /// @return
        /// </summary>
        Parameters GetLayerPara();
        /// <summary>
        /// @param 成型阶段预处理层参数
        /// </summary>
        void SetPreLayerPara(Parameters 成型阶段预处理层参数);
        /// <summary>
        /// @return
        /// </summary>
        Parameters GetPreLayerPara();
        /// <summary>
        /// @param 成型阶段辅助参数
        /// </summary>
        void SetAidPara(Parameters 成型阶段辅助参数);
        /// <summary>
        /// @return
        /// </summary>
        Parameters GetAidPara();
        void SetPlanPara();
        void GetPlanPara();
    }

    public interface IAuto
    {
        /// <summary>
        /// 根据任务管理模块发送来的单片层任务数据，依次调度辅助功能模块、单层成型预备模块和单层成型模块，完成一次单层的成型。
        /// @param 单层任务数据对象的列表
        /// </summary>
        //void BLayer(List<Layer> 单层任务数据对象的列表);//20200505新增批注：消除潜在的来自序列号类替换的后果
        void Pause();
        void Stop();
        /// <summary>
        /// @return 返回调度状态的字符串列表
        /// </summary>
        List<string> Status();

    }

    public interface IElectric
    {
        /// <summary>
        /// @return
        /// </summary>
        string CreateConfigureFile_MotionCard();
        /// <summary>
        /// @param ConfigureFile1
        /// </summary>
        void ExtractConfigureFile(string ConfigureFile1);
        /// <summary>
        /// @return
        /// </summary>
        string CreateConfugureFile_PrintingCard();   
        void OneKeyStart();
        /// <summary>
        /// @param ConfigureFile2
        /// </summary>
        void ExtractConfugureFile_PrintingCard(string ConfigureFile2);

    }

    public interface ILayerPowder
    {
        /// <summary>
        /// @return
        /// </summary>
        Parameters GetPara();
        /// <summary>
        /// @param 工艺参数
        /// </summary>
        void SetPara(Parameters 工艺参数);
        void Run();
        /// <summary>
        /// @return
        /// </summary>
        List<string> Status();
        void Pause();
        void Stop();
        /// <summary>
        /// @param 轴号 
        /// @param 方向 
        /// @param 数值
        /// </summary>
        void Jog(long 轴号, bool 方向, double 数值);
        /// <summary>
        /// @return
        /// </summary>
        List<string> GetAxises();
    }

    public interface IManual
    {
        /// <summary>
        /// 人机交互模块调用
        /// @param 轴号 
        /// @param 方向 
        /// @param 数值
        /// </summary>
        void Jog(long 轴号, bool 方向, double 数值);
        /// <summary>
        /// 人际交互模块初始化调用
        /// @return
        /// </summary>
        List<string> GetAxises();
        void 清洗喷头();
        void 粘接();
        void 供墨();
        void 正负压();
        void 温度();
        void 推出成型缸();
        void 推进成型缸();
    }

    public interface ILayerBinders
    {
        /// <summary>
        /// @return
        /// </summary>
        Parameters GetPara();
        /// <summary>
        /// @param 工艺参数
        /// </summary>
        void SetPara(Parameters 工艺参数);
        /// <summary>
        /// @param 指令数据
        /// </summary>
        void Run(List<Command> 指令数据);
        /// <summary>
        /// @return
        /// </summary>
        List<string> Status();
        void Pause();
        void Stop();
        /// <summary>
        /// @param 轴号 
        /// @param 方向 
        /// @param 数值
        /// </summary>
        void Jog(long 轴号, bool 方向, double 数值);
        /// <summary>
        /// @return
        /// </summary>
        List<string> GetAxises();
        void 喷墨();
    }

    public interface IJob
    {
        /// <summary>
        /// 执行一次，执行一次创建任务，生成一个相对的实体任务
        /// @param 数控代码存储路径
        /// </summary>
        void CreateJob(string 数控代码存储路径);
        /// <summary>
        /// @param 已创建任务编号
        /// </summary>
        void RemoveJob(long 已创建任务编号);
        /// <summary>
        /// @param 模块编号 
        /// @param 模块的相关参数对象
        /// </summary>
        void SetJobPara(short 模块编号, Parameters 模块的相关参数对象);
        /// <summary>
        /// @param 模块编号 
        /// @return
        /// </summary>
        Parameters GetJobPara(short 模块编号);
        /// <summary>
        /// （1）依次读单实体任务中位于相同切片层的片层任务数据
        /// （2）将各片层任务列表后统一发送给自动调度模块
        /// </summary>
        void Run();
    }

    public interface IGui
    {
        /// <summary>
        /// @param UserName 
        /// @param PassWord 
        /// @return
        /// </summary>
        bool Login(string UserName, string PassWord);
        void 显示2D加工截面();
        void 显示3D加工预览();
    }

    public interface IMonitor
    {
        /// <summary>
        /// @return
        /// </summary>
        List<string> GetStatus();
    }

    public interface IAmbient
    {
        /// <summary>
        /// @return
        /// </summary>
        Parameters GetPara();
        /// <summary>
        /// @param 工艺参数
        /// </summary>
        void SetPara(Parameters 工艺参数);
        void Run();
        /// <summary>
        /// @return
        /// </summary>
        List<string> Status();
        void Stop();
        /// <summary>
        /// @param 轴号 
        /// @param 方向 
        /// @param 数值
        /// </summary>
        void Jog(long 轴号, bool 方向, double 数值);
        /// <summary>
        /// @return
        /// </summary>
        List<string> GetAxises();
        void 滴墨();
        void 供墨();
        void 正负压();
        void 温度();
    }

    public interface IDataProcess
    {
        /// <summary>
        /// @param CLI 
        /// @return
        /// </summary>
        PTask ExtractCLI(string CLI);
        /// <summary>
        /// @param Parameter1
        /// </summary>
        void GetPara(PlanningParameters Parameter1);
        /// <summary>
        /// @return
        /// </summary>
        PlanningParameters SetPara();
        /// <summary>
        /// @param CLIData 
        /// @param 分辨率 
        /// @return
        /// </summary>
        string CreateTIFF(string CLIData, int 分辨率);
        /// <summary>
        /// @param Parameter1 
        /// @param 层厚 
        /// @return
        /// </summary>
        string ExtractSTL(string Parameter1, double 层厚);
        void 图片分割及搭接();

    }

    public interface IDataSet
    {
        void AddRecord();
        void DeleteRecord();
        void ChangeRecord();
        void QueryRecord();
        void UpdateDataset();
    }

    public interface IServer
    {
        void Login();
        void Exit();
        void UpdateDataset();
        void RemoteControl();
        void RequestRC();
        void StopRC();
    }
}
