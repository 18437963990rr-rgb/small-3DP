using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;


namespace ReadFile
{
    class ThreeDimension//CLI解析出的STL的最小包围体面积————尺寸的三维点
    {
        public double x, y, z;
    }

    //public interface ICloneable<T>
    //{
    //    T Clone();
    //}

    class CLI /*: ICloneable<CLI>*///CLI的数据类
    {
        public int LayerNumber;//总层数fe
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
        public string recordPathItem=null;//NEW added, because it is very important to contain this information.

        public static implicit operator List<object>(CLI v)
        {
            throw new NotImplementedException();
        }
    }

    class Layer//多条轮廓线
    {
        public double z;//每层的高度
        public List<Line> LineList = new List<Line>();//层线集合
    }
    public class Line//一条轮廓线：线的轮廓线
    {
        public int id, n, dir;//id：模型序号。n:点数。dir:轮廓线的方向。
        //public List<Point[]> Lines = new List<Point[]>();//counter
        public List<PointF[]> Lines = new List<PointF[]>();//counter
    }

    class STL
    {
        public List<CLI> STLFile=new List<CLI>();//CLI的链表

        public static CLI ReadCLI(string FileName)//读取CLI文件
        //public static CLI ReadCLI(FileStream myStream)//读取CLI文件
        {
            FileStream myStream = new FileStream(FileName, FileMode.Open, FileAccess.Read);//—————使用指定的创建路径和创建模式和读写权限来读取文件—————！！！！
            CLI Cli = new CLI();
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
                                        p.x = Convert.ToDouble(unt);
                                        unt = "";
                                        f++;
                                        //break;
                                    }
                                    else if (f % 3 == 1)
                                    {
                                        i = j;
                                        p.y = Convert.ToDouble(unt);
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
                        Lp.X = Convert.ToSingle(reader.ReadInt16() * Unint);
                        Lp.Y = Convert.ToSingle(reader.ReadInt16() * Unint);

                        Oneline[i] = Lp; ;
                    }
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
            return layer;
        }
    }
}
