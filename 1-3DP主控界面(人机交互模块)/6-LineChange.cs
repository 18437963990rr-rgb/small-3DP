using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReadFile;
using System.Drawing;

namespace BinderJetting//——————————————这段代码还要进行修改——————提高他的通用性——————！！！！！
{
    class LineChange
    {
        //line偏移
        public static Line TranslationInDirect(Line line, float X, float Y)
        {
            Line Poly = line;
            for (int i = 0; i < line.n; i++)
            {
                Poly.Lines[0][i].X += X;
                Poly.Lines[0][i].Y += Y;
            }
            return Poly;
        }

        //——————————————————————设置线左上角的X和Y
        /// <summary>
        /// 修正CLI模型中每条LineList的位置:20200506新建批注
        /// </summary>
        /// <param name="line"></param>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        public static void TranslationDirect(Line line, double X, double Y)
        {
            for (int i = 0; i < line.n; i++)
            {
                line.Lines[0][i].X += Convert.ToSingle(X);
                line.Lines[0][i].Y += Convert.ToSingle(Y);
            }
        }

        //图形便宜
        public static CLI CLITranslation(CLI cli, PointF p)
        {
            double X = (p.X - cli.Dimension[0].x);
            double Y = (p.Y - cli.Dimension[0].y);
            cli.Dimension[0].x += X;
            cli.Dimension[1].x += X;
            cli.Dimension[0].y += Y;
            cli.Dimension[1].y += Y;

            for (int i = 0; i < cli.LayerNumber; i++)
            {
                for (int j = 0; j < cli.LayerLine[i].LineList.Count; j++)
                {
                    LineChange.TranslationDirect(cli.LayerLine[i].LineList[j], X, Y);
                }
            }
            return cli;
        }

        //最大层数？
        public static int MaxLayerNumber(List<CLI> stl)
        {
            int Lyer = 0;
            for (int i = 0; i < stl.Count; i++)
            {
                if (stl[i].LayerNumber > Lyer)
                {
                    Lyer = stl[i].LayerNumber;
                }
            }
            return Lyer;
        }

        //多个图合并成一个打印的大面积图,图的长宽——————————————————————————————————还没有测试！！！！！
        public CLI GetPrintCli(List<CLI> stl, double Width, double Height)//——————————————输入的参数STL是排版好的
        {
            CLI Cli = new CLI();
            int Maxy = MaxLayerNumber(stl);
            Cli.LayerThickness = stl[0].LayerThickness;
            Cli.Dimension[1].x = Width;
            Cli.Dimension[1].y = Height;
            Cli.Dimension[1].z = Cli.LayerThickness * Maxy;

            Cli.Dimension[0].x = 0;
            Cli.Dimension[0].y = 0;
            Cli.Dimension[0].z = 0;

            Cli.LayerNumber = Maxy;
            Cli.FileStyle = stl[0].FileStyle;
            Cli.StartLayer = 0;
            Cli.Version = stl[0].Version;
            for (int i = 0; i < Maxy; i++)
            {
                for (int j = 0; j < stl.Count; j++)
                {
                    if (stl[j].StartLayer >= i)//————————————————————达到1个CLI的起始层，开始合并。
                    {
                        for (int k = 0; k < stl[j].LayerLine[i - stl[j].StartLayer].LineList.Count; k++)
                        {
                            Cli.LayerLine[i].LineList.Add(stl[j].LayerLine[i - stl[j].StartLayer].LineList[k]);
                        }
                    }
                }
            }
            return Cli;
        }


        //绕某点旋转
        public static Line Rotation(Line line, double Angle, PointF p)
        {
            Line Poly = line;
            for (int i = 0; i < line.n; i++)
            {
                Poly.Lines[0][i].X = Convert.ToSingle(Poly.Lines[0][i].X * Math.Cos(Angle) - Poly.Lines[0][i].Y * Math.Sin(Angle) +
                    p.X * (1 - Math.Cos(Angle)) + p.Y * Math.Sin(Angle)); ;
                Poly.Lines[0][i].Y = Convert.ToSingle(Poly.Lines[0][i].X * Math.Sin(Angle) + Poly.Lines[0][i].Y * Math.Cos(Angle) +
                    p.Y * (1 - Math.Cos(Angle)) - p.X * Math.Sin(Angle)); ;
            }
            return Poly;
        }
        //绕原点旋转
        public static Line Rotation(Line line, double Angle)
        {
            Line Poly = line;
            for (int i = 0; i < line.n; i++)
            {
                Poly.Lines[0][i].X = Convert.ToSingle
                    (Poly.Lines[0][i].X * Math.Cos(Angle) - Poly.Lines[0][i].Y * Math.Sin(Angle));
                Poly.Lines[0][i].Y = Convert.ToSingle(Poly.Lines[0][i].X * Math.Sin(Angle) + Poly.Lines[0][i].Y * Math.Cos(Angle));
            }
            return Poly;
        }
    }
}
