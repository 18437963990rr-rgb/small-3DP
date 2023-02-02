using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BinderJetting
{
    [Serializable]
    public class PrinterSysParam //20200222：
    {
        //public double[] m_Svel = { 20.0, 20.0, 20.0, 20.0 }/*new double[4]*/;//20mm/s
        //public double[] m_Step = { 0.03, 0.03, 0.03, 0.03 } /*new double[4]*/;
        //public bool[] m_bEnds = { true, true, true, true }/*new bool[4]*/;
        //public bool[] m_bMoveModeFlag = { true, true, true, false }/*new bool[4]*/;
        //(1)存储6个电机的校准行程
        public double[] g_dJourney = new double[6];//20200222：存储的6个电机的校准行程，也是0位的编码器值；单位：脉冲

        public double[] g_dPositon = new double[7];//20200514：存储7个电机的位置（前6个轴为铺粉相关，第7个轴双X轴）；单位：毫米
        ////断电现场时间及断电标志：
        //public string DT = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");//=DateTime.Now.ToString();
        //public bool PowerOff = true;

    }
}
