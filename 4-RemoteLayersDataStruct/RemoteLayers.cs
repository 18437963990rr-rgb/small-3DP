using System;
using System.Collections.Generic;
using System.Drawing;


//临时不需要使用新的引用设置：20200502新增批注：
//（1）Remote object.
public class RemoteObject : MarshalByRefObject
{
    private int callCount = 0;
    private double progressValue = 0;//进度安排：20200504新增
    private int r_ControlFlag = 0;//20200507新增：
    private int r_ReturnFlag = 0;//20200507新增：
    //private List<PointCoordinate> LocalPointCoordinates;
    private /*private*/ PointCoordinates LocalPointCoordinates = new PointCoordinates();//LocalPointCoordinates.g_LayerDatas存放所有的数据
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
    public bool SendPosition(RemoteCLIs remoteCLIs, int LayerIndex/*PointCoordinates/*List<PointCoordinate>pointCoordinates*/)//API:发送排版队列
    {
        ////(2)附加远程信息结构
        //RemoteCLIs tempRemoteCLIs = new RemoteCLIs();
        //tempRemoteCLIs.layerIndex = LayerIndex;
        //tempRemoteCLIs.aLayerData.Add(tempCountCLI);
        //(3)远程发送
        LocalPointCoordinates.g_LayerDatas.Add(remoteCLIs);

        /*PointCoordinate[] */
        //LocalPointCoordinates = pointCoordinates;
        return true;//返回到本地
    }
    /// <summary>
    /// API:获取所有的排版数据：20200506新建
    /// </summary>
    /// <returns></returns>
    public PointCoordinates GetPositionData()
    {
        //(1)远程读取到本地
        return LocalPointCoordinates;//返回到本地
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

    public void SendControlCommand(int controlFlag, int returnFlag)
    {
        r_ControlFlag = controlFlag;//r_代表远程
        r_ReturnFlag = returnFlag;//r_代表远程
    }
    public void ReadControlCommand(ref int controlFlag, ref int returnFlag)
    {
        controlFlag = r_ControlFlag;
        returnFlag = r_ReturnFlag;
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

/******************************************20200504数据测试:******************************************/
/******************************************20200504数据测试:******************************************/
[Serializable()]
public class PointCoordinates//坐标变量:所有层的所有零件的加工数据
{
    public List<PointCoordinate> g_PointCoordinates = new List<PointCoordinate>();
    public List<RemoteCLIs> g_LayerDatas = new List<RemoteCLIs>();//2020506新建：此处保存所有层的数据。
}
[Serializable()]
public class PointCoordinate//坐标变量
{
    public double x;//X坐标
    public double y;//Y坐标
}


/******************************************20200505新增开始:******************************************/
/******************************************20200505新增开始:******************************************/
//20200505新增：
[Serializable()]
public class ThreeDimension//CLI解析出的STL的最小包围体面积————尺寸的三维点//20200505新增：从6-ReadFile中移植过来
{
    public double x, y, z;
}
[Serializable()]
public class Layer//多条轮廓线//20200505新增：从6-ReadFile中移植过来
{
    public double z;//每层的高度
    public List<Line> LineList = new List<Line>();//层线集合
}
[Serializable()]
public class Line//一条轮廓线：线的轮廓线//20200505新增：从6-ReadFile中移植过来
{
    public int id, n, dir;//id：模型序号。n:点数。dir:轮廓线的方向。
                          //public List<Point[]> Lines = new List<Point[]>();//counter
    public List<PointF[]> Lines = new List<PointF[]>();//counter
    public List<ThreeDimension> Dimension = new List<ThreeDimension>();//20201209新增修改：存放值为本条Line的包围盒
    public int GenerationCode = 0;//20201209新增修改:0为默认值（无效），1表示第1代，2表示第2代，3表示第3代，依次类推（有效数据从1开始）。。。
}
/// <summary>
/// CLI类：只要实现当层cli对象的跨进程传输，所有的事情即可搞定：20200505新建批注
/// </summary>
[Serializable()]
public class CLI /*: ICloneable<CLI>*///CLI的数据类//20200505新增：从6-ReadFile中移植过来
{
    public int LayerNumber;//总层数
    public string FileStyle;//文件风格二进制或ASCII
    public double Units;//单位
    public int Version;//版本号
    public double LayerThickness;//层厚
                                 //public List<ThreeDimension> Dimension = new List<ThreeDimension>();//实体最小外接立方体的对角点（x，y，z）————三维尺寸：两个三维点
    public List<Layer> LayerLine = new List<Layer>();//层数据链表————三维尺寸：层的一条轮廓线的数据
    public int StartLayer;//开始的层数

    //NEW added, because it is very important to contain this information.
    public List<ThreeDimension> Dimension = new List<ThreeDimension>();//实体最小外接立方体的对角点（x，y，z）————三维尺寸：两个三维点
                                                                       //public double PositionX=0, PositionY=0;//NEW added, because it is very important to contain this information.——使用DImension即可
    public int ID = 0;//NEW added, important
    public string recordPathItem = null;//NEW added, because it is very important to contain this information.

    //20200505新增：消除隐式方法，应该对主界面中List <CLI>的使用没有影响
    //20200505新增：消除隐式方法，应该对主界面中List <CLI>的使用没有影响
    //20200505新增：消除隐式方法，应该对主界面中List <CLI>的使用没有影响
    //public static implicit operator List<object>(CLI v)
    //{
    //    throw new NotImplementedException();
    //}
}
/******************************************20200505新增结束:******************************************/
/******************************************20200505新增结束:******************************************/

/******************************************20200506新增开始:******************************************/
/******************************************20200506新增开始:******************************************/

/// <summary>
/// RemoteCLIs:保存1层所有LineList的数据：20200506新建
/// </summary>
[Serializable()]
public class RemoteCLIs
{
    public int layerIndex;//20200506新建：1层的索引
    public List<CountCLI> aLayerData = new List<CountCLI>();//20200506新建：存放1层的所有零件的Cli数据
}

/// <summary>
/// 
/// </summary>
[Serializable()]
public class CountCLI
{
    public Layer aLayer = new Layer();
    public float x = 0;//附加给aLayer的排版位置信息
    public float y = 0;//附加给aLayer的排版位置信息
    public float deltaX = 0;//偏移值x：20201113新增
    public float deltaY = 0;//偏移值x：20201113新增
    public float width = 0;//20201108新增：附加给aLayer的宽度
    public float height = 0;//20201108新增：附加给aLayer的高度
} 

/******************************************20200506新增结束:******************************************/
/******************************************20200506新增结束:******************************************/
