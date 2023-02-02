using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//临时不需要使用新的引用设置：20200502新增批注：
//（1）Remote object.
public class RemoteObject : MarshalByRefObject
{
    private int callCount = 0;
    private double progressValue = 0;//进度安排：20200504新增

    //private List<PointCoordinate> LocalPointCoordinates;
    private PointCoordinates LocalPointCoordinates = new PointCoordinates();
    /// <summary>
    /// API:计数测试
    /// </summary>
    /// <returns></returns>
    public int GetCount()//（1）API1:计数测试
    {
        //Console.WriteLine("GetCount has been called.");
        callCount++;
        return (callCount);
    }
    /// <summary>
    /// API:发送排版信息
    /// </summary>
    /// <param name="index"></param>
    /// <param name="end"></param>
    /// <param name="AimPath"></param>
    /// <param name="坐标地址"></param>
    /// <returns></returns>
    public bool SendPosition(PointCoordinates/*List<PointCoordinate>*/ pointCoordinates)//API:发送排版队列
    {
        /*PointCoordinate[] */
        LocalPointCoordinates = pointCoordinates;
        return true;//返回到本地
    }
    /// <summary>
    /// API:生成BMPfile，在指定文件夹
    /// </summary>
    /// <param name="ClisVector：Cli数组的动态数组的指针"></param>
    /// <param name="filePath：生成BMPfile的保存文件夹"></param>
    /// <returns></returns>
    public bool CreatBmpfile(IntPtr ClisVector,/*PointCoordinate[] LocalPointCoordinates,*/string filePath)
    {
        return true;
    }

    /// <summary>
    /// 服务器端API：从服务器端设置数据处理进度值：20200504新增
    /// </summary>
    /// <param name="dynamicValue"></param>
    public void SetBackValue(double dynamicValue)//服务器端API：从服务器端设置数据处理进度值：20200504新增
    {
        progressValue = dynamicValue;
    }
    /// <summary>
    /// 客户端API：从客户端读取数据处理进度值：20200504新增
    /// </summary>
    /// <returns></returns>
    public double GetBackValue()//客户端API：从客户端读取数据处理进度值：20200504新增
    {
        return progressValue;
    }
    /// <summary>
    /// API:获取排版队列
    /// </summary>
    /// <returns></returns>
    public PointCoordinates/*List<PointCoordinate>*/ GetPosition()//API:获取排版队列
    {
        PointCoordinates/*List<PointCoordinate>*/ returnPointCoordinates = LocalPointCoordinates;
        return returnPointCoordinates;//返回到本地
    }
}

[Serializable()]
public class PointCoordinates//坐标变量
{
    public List<PointCoordinate> g_PointCoordinates = new List<PointCoordinate>();
}
[Serializable()]
public class PointCoordinate//坐标变量
{
    public double x;//X坐标
    public double y;//Y坐标
}
