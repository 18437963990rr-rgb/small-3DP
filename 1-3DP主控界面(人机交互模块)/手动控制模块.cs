using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _3DP_KeyLibrary;

namespace BinderJetting
{
    class 手动控制模块 : IManual
    {
        public List<string> GetAxises()
        {
            throw new NotImplementedException();
        }

        public void Jog(long 轴号, bool 方向, double 数值)//数值为速度
        {
            throw new NotImplementedException();
        }

        public void 供墨()
        {
            throw new NotImplementedException();
        }

        public void 推出成型缸()
        {
            throw new NotImplementedException();
        }

        public void 推进成型缸()
        {
            throw new NotImplementedException();
        }

        public void 正负压()
        {
            throw new NotImplementedException();
        }

        public void 清洗喷头()
        {
            throw new NotImplementedException();
        }

        public void 温度()
        {
            throw new NotImplementedException();
        }

        public void 粘接()
        {
            throw new NotImplementedException();
        }
    }
}
