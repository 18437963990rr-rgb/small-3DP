using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;


namespace ReadFile
{
    /******************************************20200505删减开始:******************************************/
    /******************************************20200505删减开始:******************************************/
    //20200505新增：从6-ReadFile中移植到RemoteLayers
    //20200505新增：从6-ReadFile中移植到RemoteLayers
    //20200505新增：从6-ReadFile中移植到RemoteLayers

    //class ThreeDimension//CLI解析出的STL的最小包围体面积————尺寸的三维点
    //{
    //    public double x, y, z;
    //}
    //class Layer//多条轮廓线
    //{
    //    public double z;//每层的高度
    //    public List<Line> LineList = new List<Line>();//层线集合
    //}
    //public class Line//一条轮廓线：线的轮廓线
    //{
    //    public int id, n, dir;//id：模型序号。n:点数。dir:轮廓线的方向。
    //    //public List<Point[]> Lines = new List<Point[]>();//counter
    //    public List<PointF[]> Lines = new List<PointF[]>();//counter
    //}
    ///// <summary>
    ///// CLI类：只要实现当层cli对象的跨进程传输，所有的事情即可搞定：20200505新建批注
    ///// </summary>
    //class CLI /*: ICloneable<CLI>*///CLI的数据类
    //{
    //    public int LayerNumber;//总层数fe
    //    public string FileStyle;//文件风格二进制或ASCII
    //    public double Units;//单位
    //    public int Version;//版本号
    //    public double LayerThickness;//层厚
    //    //public List<ThreeDimension> Dimension = new List<ThreeDimension>();//实体最小外接立方体的对角点（x，y，z）————三维尺寸：两个三维点
    //    public List<Layer> LayerLine = new List<Layer>();//层数据链表————三维尺寸：层的一条轮廓线的数据
    //    public int StartLayer;//开始的层数

    //    //NEW added, because it is very important to contain this information.
    //    public List<ThreeDimension> Dimension = new List<ThreeDimension>();//实体最小外接立方体的对角点（x，y，z）————三维尺寸：两个三维点
    //    //public double PositionX=0, PositionY=0;//NEW added, because it is very important to contain this information.——使用DImension即可
    //    public int ID = 0;//NEW added, important
    //    public string recordPathItem=null;//NEW added, because it is very important to contain this information.

    //    //20200505新增：消除隐式方法，应该对主界面中List <CLI>的使用没有影响
    //    //20200505新增：消除隐式方法，应该对主界面中List <CLI>的使用没有影响
    //    //20200505新增：消除隐式方法，应该对主界面中List <CLI>的使用没有影响
    //    //public static implicit operator List<object>(CLI v)
    //    //{
    //    //    throw new NotImplementedException();
    //    //}
    //}
    /******************************************20200505删减开始:******************************************/
    /******************************************20200505删减开始:******************************************/


    class STL
    {
        public List<CLI> STLFile=new List<CLI>();//CLI的链表

        public static CLI ReadCLI(string FileName)//读取CLI文件
        //public static CLI ReadCLI(FileStream myStream)//读取CLI文件
        {
            FileStream myStream = new FileStream(FileName, FileMode.Open, FileAccess.Read);//—————使用指定的创建路径和创建模式和读写权限来读取文件—————！！！！
            CLI Cli = new CLI();
            Cli.recordPathItem = FileName;//20201110新增：保存原始文件路径信息
            BinaryReader reader = new BinaryReader(myStream);
            string str = "";
            string End = "$$HEADEREND";

            while (true)
            {
                str = "";
                while (reader.PeekChar() != -1)
                {
                    if (str == End)
                        break;
                
                    char ch = reader.ReadChar();
                    if (ch != '\n')
                    {
                        str += ch;
                    }
                    else { break; }
                }

                string strName = "";
                strName += str[0]; strName += str[1];
                strName += str[2]; strName += str[3];
                strName += str[4]; strName += str[5];
                strName += str[6];
                if (str == "$$HEADEREND")
                {
                    break;
                }
                else if (strName == "$$VERSI")
                {
                    for (int i = 0; i < str.Length; i++)
                    {
                        if (str[i] == '/')
                        {
                            string unt = "";
                            for (int j = i + 1; j < str.Length; j++)
                            {
                                unt += str[j];
                            }
                            Cli.Version = Convert.ToInt16(unt);
                            break;
                        }
                    }
                }
                else if (str == "$$BINARY")
                {
                    Cli.FileStyle = "Binary";
                }
                else if (str == "$$ASCII")
                {
                    Cli.FileStyle = "ASCII";
                }
                else if (strName == "$$UNITS")
                {
                    for (int i = 0; i < str.Length; i++)
                    {
                        if (str[i] == '/')
                        {
                            string unt = "";
                            for (int j = i + 1; j < str.Length; j++)
                            {
                                unt += str[j];
                            }
                            Cli.Units = Convert.ToDouble(unt);
                            break;
                        }
                    }
                }
                else if (strName == "$$DIMEN")
                {
                    for (int i = 0; i < str.Length; i++)
                    {
                        if (str[i] == '/')
                        {
                            int f = 0, d = 2;
                            string unt = "";

                            while (i < str.Length - 1)
                            {
                                ThreeDimension p = new ThreeDimension();
                                for (int j = i + 1; j < str.Length; j++)
                                {
                                    if (str[j] != ',')
                                    {
                                        unt += str[j];
                                        if (j == str.Length - 1)
                                        {
                                            i = j;
                                            p.z = Convert.ToDouble(unt);
                                            f++;
                                            d = 3;
                                            break;
                                        }
                                    }
                                    else if (f % 3 == 0)
                                    {
                                        i = j;
                                        p.x = Convert.ToDouble(unt);//20220915修改：X方向打印数据反向：添加负号：未修正
                                        unt = "";
                                        f++;
                                        //break;
                                    }
                                    else if (f % 3 == 1)
                                    {
                                        i = j;
                                        p.y = -Convert.ToDouble(unt);//20220915修改：X方向打印数据反向：添加负号
                                        unt = "";
                                        f++;
                                        // break;
                                    }
                                    else if (f % 3 == 2)
                                    {
                                        i = j;
                                        p.z = Convert.ToDouble(unt);
                                        f++;
                                        unt = "";
                                        d = 3;
                                        break;
                                    }
                                }
                                if (f % 3 == 0 && d % 3 == 0)
                                {
                                    Cli.Dimension.Add(p);
                                }
                            }
                            break;
                        }
                    }
                }
                else if (strName == "$$LAYER")
                {
                    for (int i = 0; i < str.Length; i++)
                    {
                        if (str[i] == '/')
                        {
                            string unt = "";
                            for (int j = i + 1; j < str.Length; j++)
                            {
                                unt += str[j];
                            }
                            Cli.LayerNumber = Convert.ToInt32(unt);
                            break;
                        }
                    }
                }
            }
            long Pos = myStream.Position;
            while (myStream.Position < myStream.Length)
            {
                Layer alayer = ReadOneLayer(myStream.Position, myStream, Cli.Units);
                Cli.LayerLine.Add(alayer);
            }
            myStream.Close();
            if (Cli.LayerLine.Count() > 1)
            {
                Cli.LayerThickness = Cli.LayerLine[1].z - Cli.LayerLine[0].z;
                Cli.StartLayer = Convert.ToInt32(Cli.LayerLine[0].z / Cli.LayerThickness);//开始层
            }
            else
            {
                Cli.LayerThickness = Cli.LayerLine[0].z;
                Cli.StartLayer = 1;//开始层为1
            }
            //数据处理
            DealCLI(Cli);//处理CLI
            return Cli;//最后保存到CLI文件
        }

        public static void DealCLI(CLI Cli)//处理全部的CLI数据
        {
            for (int c = 0; c < Cli.LayerNumber; c++)
            {
                for (int L = 0; L < Cli.LayerLine[c].LineList.Count; L++)
                {
                    if (Cli.LayerLine[c].LineList[L].dir != 2)
                    {
                        Line line = Cli.LayerLine[c].LineList[L];
                        DealLine(line);//删除多余点
                        Cli.LayerLine[c].LineList[L] = line;
                    }
                }
            }
        }

        public static void DealLine(Line Ln)//处理每条线多余的点
        {
            bool Fg = true;
            while (Fg)
            {
                Fg = false;
                int n = Ln.Lines[0].Length;
                for (int i=0;i<Ln.Lines[0].Length;i++)
                {
                    if (SamePoint(Ln.Lines[0][i],Ln.Lines[0][(i + 1) % n])&&n>1)
                    {
                        DeletePoint(Ln, i);
                        Fg = true;
                        break;
                    }
                    else if (TreeOneLine(Ln.Lines[0][i], Ln.Lines[0][(i + 1) % n], Ln.Lines[0][(i + 2) % n])&&n>2)
                    {
                        DeletePoint(Ln,(i+1)%n);
                        Fg = true;
                        break;
                    }
                    else { }
                }
            }
            Ln.n = Ln.Lines[0].Length;
        }

        public static  void DeletePoint(Line line,int i)//删除多余点，删除重复点
        {
            int n = line.Lines[0].Length;
            //Point[] poly = new Point[n-1];
            PointF[] poly = new PointF[n - 1];

            for (int m=0;m<n;m++)
            {
                if (m < i)
                { poly[m] = line.Lines[0][m]; }
                else if (m > i)
                { poly[m - 1] = line.Lines[0][m]; }
                else { }
            }
            line.Lines[0] = poly;
        }

        public static bool TreeOneLine(PointF one, PointF Two, PointF Three)//判断三点是否在同一条线
        {
            //Point OneTwo = new Point();
            PointF OneTwo = new PointF();

            OneTwo.X = Two.X - one.X;
            OneTwo.Y = Two.Y - one.Y;
            double ZTwo = Math.Sqrt(OneTwo.X * OneTwo.X + OneTwo.Y * OneTwo.Y);

            //Point OneThree = new Point();
            PointF OneThree = new PointF();
            OneThree.X = Three.X - one.X;
            OneThree.Y = Three.Y - one.Y;

            double ZThree = Math.Sqrt(OneThree.X * OneThree.X + OneThree.Y * OneThree.Y);
            double Cha = (OneTwo.X * OneThree.Y - OneTwo.Y * OneThree.X) / (ZTwo * ZThree);
            //if (Math.Abs(Cha) < 0.01) { }
            if (Math.Abs(Cha) < 0.001)
            { return true; }
            else
            { return false; }
        }
        //public static bool SamePoint(Point p,Point p1)//判断相同点
        public static bool SamePoint(PointF p,PointF p1)//判断相同点
        {
            double x = p1.X - p.X;
            double y = p1.Y- p.Y;
            double L = Math.Sqrt(x*x+y*y);//通过弦长来控制精度

            if (L <= 0.01)//通过弦长来控制精度
            {
                return true;
            }
            else { return false; }
        }
        public static Layer ReadOneLayer(long position, FileStream myStream, double Unint)//读取1层的数据
        {
            Layer layer = new Layer();
            long poa = position;
            BinaryReader reader = new BinaryReader(myStream);
            myStream.Seek(position, 0);
            int ch1 = reader.ReadInt16();
            if (ch1 == 128)
            {
                ch1 = reader.ReadInt16();
                layer.z = ch1 * Unint;
                poa += 4;
            }
            else
            {
                return layer;
            }
            while (poa < myStream.Length)
            {
                ch1 = reader.ReadInt16();
                Line line = new Line();
                if (ch1 == 129)
                {
                    ch1 = reader.ReadInt16();
                    line.id = ch1;
                    ch1 = reader.ReadInt16();
                    line.dir = ch1;
                    ch1 = reader.ReadInt16();
                    line.n = ch1;
                    PointF[] Oneline = new PointF[line.n];
                    for (int i = 0; i < line.n; i++)
                    {
                        PointF Lp = new PointF();
                        //Lp.X = Convert.ToInt32(reader.ReadInt16() * Unint);
                        //Lp.Y = Convert.ToInt32(reader.ReadInt16() * Unint);
#if false//fengqiangbug
                        int x = reader.ReadInt16();
                        int y = reader.ReadInt16();
#else//20200514xiufubug
                        int x = reader.ReadUInt16();
                        int y = reader.ReadUInt16();
#endif
                        Lp.X = Convert.ToSingle(/*reader.ReadInt16() */ x* Unint);//20220915修改：X方向打印数据反向：添加负号；未修正
                        Lp.Y = -Convert.ToSingle(/*reader.ReadInt16() */ y* Unint);//20220915修改：X方向打印数据反向：添加负号

                        Oneline[i] = Lp; ;
                    }
                    //line.Dimension[0] =;//20201209新增：本条轮廓包围盒的左上角值
                    //line.Dimension[1] =;//20201209新增：本条轮廓包围盒的右下角值
                    //line.Dimension = CalcuteContainAera(Oneline);//20201209新增：
                    CalcuteContainAera(Oneline, line.Dimension);//20201209新增：计算出各条轮廓的标志环

                    line.Lines.Add(Oneline);
                    layer.LineList.Add(line);
                    poa = myStream.Position;
                }
                else if (ch1 == 128)
                {
                    position = myStream.Position - 2;
                    myStream.Seek(position, 0);
                    break;
                }
            }
            CalcuteGeneration(layer);//20201209新增：计算各条Line的代数
            return layer;
        }
        public static void CalcuteContainAera( PointF[] tempOneline, List<ThreeDimension> Dimension)//20201209新增：计算Line的标志包围盒
        {
            double maxX = 0; double maxY = 0; double minX = 0; double minY = 0;//临时变量
            maxX = tempOneline[0].X;
            maxY = tempOneline[0].Y;
            minX = tempOneline[0].X;
            minY = tempOneline[0].Y;
            for (int m = 0; m < tempOneline.Count(); m++)//20201113新增：更新最大区域
            {  
                if (maxX < tempOneline[m].X)//更新最大尺寸
                {
                    maxX = tempOneline[m].X;
                }
                else { }
                if (maxY < tempOneline[m].Y)//更新最大尺寸
                {
                    maxY = tempOneline[m].Y;
                }
                else { }
                if (minX > tempOneline[m].X)//更新最小尺寸
                {
                    minX = tempOneline[m].X;
                }
                else { }
                if (minY > tempOneline[m].Y)//更新最小尺寸
                {
                    minY = tempOneline[m].Y;
                }
                else { }
            }
            //maxX = maxX - minX; maxY = maxY - minY;
            //ThreeDimension[] Dimension = new ThreeDimension[2] ;//20201209新增修改：存放值为本条Line的包围盒
            ThreeDimension tempDimension = new ThreeDimension();
            tempDimension.x = minX;
            tempDimension.y = minY;
            Dimension.Add(tempDimension);
            ThreeDimension tempDimension2 = new ThreeDimension();
            tempDimension2.x = maxX;
            tempDimension2.y = maxY;
            Dimension.Add(tempDimension2);
            //return Dimension;
        }

        public static void CalcuteGeneration(Layer layer)//20201209新增：计算Line的代数
        {
            //20201209批注：Calcute the right sequence for each Line;
            for (int i=0;i< layer.LineList.Count();i++)//遍历所有的Line,计算Line的被包围次数，即为本条Line的代数
            {
                int GenerationCode = 0;
                for (int j = 0; j < layer.LineList.Count(); j++)
                {
                    if ((layer.LineList[i].Dimension[0].x >= layer.LineList[j].Dimension[0].x) &&
                        (layer.LineList[i].Dimension[0].y >= layer.LineList[j].Dimension[0].y) &&
                        (layer.LineList[i].Dimension[1].x <= layer.LineList[j].Dimension[1].x) &&
                        (layer.LineList[i].Dimension[1].y <= layer.LineList[j].Dimension[1].y))
                    {
                        GenerationCode++;
                    }
                    else { }
                    layer.LineList[i].GenerationCode = GenerationCode;
                }
            }
            //20201209批注：Exchange TheLineNode with the right sequence;
            {
                int MaxGeneration = 0;
                MaxGeneration = layer.LineList[0].GenerationCode;
                for (int i = 0; i < layer.LineList.Count(); i++)//筛选出所有Node的最大代数
                {
                    if (MaxGeneration < layer.LineList[i].GenerationCode)
                    {
                        MaxGeneration = layer.LineList[i].GenerationCode;
                    }
                }

                //20201209新增：依据代数完成Line内部所有Node的重新排序
                layer.LineList.Sort((a, b) => a.GenerationCode.CompareTo(b.GenerationCode));//从1代排到2代，3代，n代。。。
            }
        }

    }
}
