using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SystemParameterSeting
{ 

    public class MotorParameter
    {
        public double 千脉冲行程 { get; set; }
        public double 周脉冲数 { get; set; }

        public double 加速度 { get; set; }
        public double 减加速度 { get; set; }
        public double 速度 { get; set; }
    }
   public class BJParameter
    {
        public double 正压力值 { get; set; }
        public double 负压力值 { get; set; }
        public double UV功率 { get; set; }
    }
    class SystemParameter
    {
        public List<MotorParameter> 成型缸参数 { get; set; }
        public List<MotorParameter> 粉料缸1参数 { get; set; }
        public List<MotorParameter> 粉料缸2参数{ get; set; }
        public List<MotorParameter> 铺粉参数 { get; set; }
        public List<MotorParameter> 锁紧参数 { get; set; }
        public List<MotorParameter> 刮液参数 { get; set; }
        public List<MotorParameter> 导轨1参数 { get; set; }
        public List<MotorParameter> 导轨2参数 { get; set; }
        public List<BJParameter> 喷墨系统参数 { get; set; }
    }
    //输出的系统参数
    class GetSystemParameter
    {
        public static SystemParameter Parameter = new SystemParameter(); 
    }
}
