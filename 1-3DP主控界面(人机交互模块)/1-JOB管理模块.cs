using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _3DP_KeyLibrary;

namespace JOB管理_调度类库_JOB管理模块_JOB调度模块
{
    public class JOB管理模块 : IJob
    {
        public void CreateJob(string 数控代码存储路径)
        {
            //读取一个JOB并建立起对应的映射————所为映射，即是在内存的一块永久存在的可调用数据



            throw new NotImplementedException();
        }

        public Parameters GetJobPara(short 模块编号)
        {
            //单独读取每个JOB文件的参数

            throw new NotImplementedException();
        }

        public void RemoveJob(long 已创建任务编号)
        {
            //JOBS是最大的单位
            //主要是删掉其中的JOB文件。
            //JOB是最大的单位，一个JOB对应的一个加工零件STL模型。
            //

            throw new NotImplementedException();
        }

        //看是否需要用到状态机的实现方式————要使用状态机实现
        //run的过程，本质是一个状态的切换过程
        //run也是本程序的一项核心功能——配的上状态机
        public void Run()
        {
            //JOB管理的运行机制，主要在这里执行。
            //负责完成CLI和TIFF的导入和生成：其中，CLI的导入是一次性的；CLI转TIFF是多次的；TIFF的导出加工装载进行的。
            //中间用到线程机制。


            throw new NotImplementedException();
        }

        public void SetJobPara(short 模块编号, Parameters 模块的相关参数对象)
        {
            //单独设置JOB的参数

            throw new NotImplementedException();
        }
    }
}
