using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using ReadFile;
using System.Drawing.Drawing2D;

using BinderJetting;//——————————此命名空间看是否需要，应该是不需要——————————！！！！！

namespace GetTiff
{
    class Tiff
    {
        public static bool StopOut = false;//停止生成————停止生产标志位
        public static int StartLayer=0;//开始生成的层数
        public static void SetTiff(Layer layer, double LayerThickness, double x, double y, string FilePath)//————输出TIFF图形
        {
            //定义图形长宽
            int X = Convert.ToInt32(x * 600 / 25.4);
            int Y = Convert.ToInt32(y * 600 / 25.4);

            double X1 = x * 600 / 25.4;
            double Y1 = y * 600 / 25.4;
            if (X < X1)//保证图像尺寸，X方向值不失真
            {
                X += 1;
            }
            if (Y < Y1)//保证图像尺寸，Y方向值不失真
            { Y += 1; }

            Bitmap bmp = new Bitmap(X, Y);
            //Bitmap bmp = new Bitmap(5000,5000);
            //设置分辨率
            bmp.SetResolution(600, 600);
            //bmp.MakeTransparent();

            //GDI+，对象
            Graphics g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;  //使绘图质量最高，即消除锯齿
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality;
            //画笔，绘制轮廓线
            //Pen OuterPen = new Pen(Color.Black, 5);
            //Pen InterPen = new Pen(Color.Black, 5);
            Pen OuterPen = new Pen(Color.Blue, 5);
            Pen InterPen = new Pen(Color.Green, 5);
            Pen OneLine = new Pen(Color.Black, 3);
            //画刷，内部填充刷
            //Brush BrushOuter = new SolidBrush(Color.Yellow);
            Brush BrushOuter = new SolidBrush(Color.Green);
            Brush BrushInter = new SolidBrush(Color.White);

            Line lines = new Line();
            for (int j = 0; j < layer.LineList.Count; j++)//float值，换算成像素值
            {
                lines = layer.LineList[j];

                PointF[] poly = new PointF[lines.Lines[0].Length];
                lines.Lines[0].CopyTo(poly,0);

                for (int k = 0; k < poly.Length; k++)
                {
                    poly[k].X = poly[k].X * Convert.ToInt32(600 / 25.4);

                    poly[k].Y = poly[k].Y * Convert.ToInt32(600 / 25.4);
                }
                if (lines.dir == 1)//
                {
                    if (poly.Length < 3)
                    {
                        if (poly.Length == 1)
                        {
                            PointF[] py = new PointF[2];
                            py[0] = poly[0]; py[1] = poly[0];
                            g.DrawLine(OuterPen, py[0].X, py[0].Y, py[1].X, py[1].Y);

                        }
                        else { g.DrawLine(OuterPen, poly[0].X, poly[0].Y, poly[1].X, poly[1].Y); }
                    }
                    else
                    {
                        g.FillPolygon(BrushOuter, poly);//输出轮廓内部填充
                    }

                }
                else if (lines.dir == 0)
                {
                    if (poly.Length < 3)
                    {
                        if (poly.Length == 1)
                        {
                            PointF[] py = new PointF[2];
                            py[0] = poly[0]; py[1] = poly[0];
                            //g.DrawLine(InterPen, py[0], py[1]);
                            g.DrawLine(InterPen, py[0].X, py[0].Y, py[1].X, py[1].Y);
                        }
                        else
                        {
                            g.DrawLine(InterPen, poly[0].X, poly[0].Y, poly[1].X, poly[1].Y);
                        }
                    }
                    else
                    {
                        g.FillPolygon(BrushInter, poly);//输出轮廓内部填充
                    }
                }
                else
                {
                    if (poly.Length < 3)
                    {
                        if (poly.Length == 1)
                        {
                            PointF[] py = new PointF[2];
                            py[0] = poly[0]; py[1] = poly[0];
                            g.DrawLine(OneLine, py[0].X, py[0].Y, py[1].X, py[1].Y);
                        }
                        else { g.DrawLine(OneLine, poly[0].X, poly[0].Y, poly[1].X, poly[1].Y); }
                    }
                    else
                    {
                        g.DrawPolygon(OneLine, poly);//输出轮廓线
                    }
                }
            }
            int num = Convert.ToInt16(layer.z / LayerThickness);
            bmp.Save(FilePath + Convert.ToString(num) + ".tiff", ImageFormat.Tiff);//————保存到TIFF文件
            g.Dispose();//————————————————————————释放画笔
            bmp.Dispose();//————————————————————————释放bmp文件
        }

        public static void InitBigTiff(int DPI,Graphics g,Pen OuterPen, Brush BrushOuter, Pen InterPen, Brush BrushInter, Pen OneLine)
        {
            Bitmap outputBMP = new Bitmap(420 * DPI, 350 * DPI);//图片大小为                                                            
            outputBMP.SetResolution(600, 600);//设置分辨率
            //outputBMP.SetPixel(420 * DPI, 350 * DPI, Color.FromArgb(0, 0, 0));//图片的像素大小，RGB完全透明
            g = Graphics.FromImage(outputBMP);//GDI+，对象
            g.SmoothingMode = SmoothingMode.AntiAlias;  //使绘图质量最高，即消除锯齿
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality;
            //画笔，绘制轮廓线
            OuterPen = new Pen(Color.Blue, 5);
            InterPen = new Pen(Color.Green, 5);
            OneLine = new Pen(Color.Black, 3);
            //画刷，内部填充刷
            BrushOuter = new SolidBrush(Color.Green);
            BrushInter = new SolidBrush(Color.White);
        }
        /// <summary>
        /// GDI+绘制1层图的操作，不包括输出到本地保存部分
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="FilePathdouble"></param>
        /// <param name="layerID"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="LayerThickness"></param>
        /// <param name="g"></param>
        /// <param name="OuterPen"></param>
        /// <param name="BrushOuter"></param>
        /// <param name="InterPen"></param>
        /// <param name="BrushInter"></param>
        /// <param name="OneLine"></param>
        public static void SetABigTiff(Layer layer, string FilePathdouble,int layerID,float x,float y, double LayerThickness,Graphics g,
            Pen OuterPen, Brush BrushOuter, Pen InterPen, Brush BrushInter,Pen OneLine)//————输出TIFF图形
        {
            //for (int j = 0; j < layer.LineList.Count; j++)//所有的坐标值偏移x,y:分别+x,+y
            //{
            //    Line lines = layer.LineList[j];
            //    PointF[] poly = lines.Lines[0];
            //    for (int k=0;k<lines.n;k++)
            //    {
            //        poly[k].X = poly[k].X + x;
            //        poly[k].Y = poly[k].Y + y;
            //    }
            //}
            for (int j = 0; j < layer.LineList.Count; j++)//float值，换算成像素值
            {
                Line lines = layer.LineList[j];
                //PointF[] poly = lines.Lines[0];

                PointF[] poly = new PointF[lines.Lines[0].Length];
                lines.Lines[0].CopyTo(poly, 0);


                for (int k = 0; k < poly.Length; k++)
                {
                    poly[k].X = (poly[k].X+x) * Convert.ToInt32(600 / 25.4);//进行了位置的偏移

                    poly[k].Y = (poly[k].Y+y) * Convert.ToInt32(600 / 25.4);//进行了位置的偏移
                }
                if (lines.dir == 1)//
                {
                    if (poly.Length < 3)
                    {
                        if (poly.Length == 1)
                        {
                            PointF[] py = new PointF[2];
                            py[0] = poly[0]; py[1] = poly[0];
                            g.DrawLine(OuterPen, py[0].X, py[0].Y, py[1].X, py[1].Y);

                        }
                        else { g.DrawLine(OuterPen, poly[0].X, poly[0].Y, poly[1].X, poly[1].Y); }
                    }
                    else
                    {
                        g.FillPolygon(BrushOuter, poly);//输出轮廓内部填充
                    }

                }
                else if (lines.dir == 0)
                {
                    if (poly.Length < 3)
                    {
                        if (poly.Length == 1)
                        {
                            PointF[] py = new PointF[2];
                            py[0] = poly[0]; py[1] = poly[0];
                            //g.DrawLine(InterPen, py[0], py[1]);
                            g.DrawLine(InterPen, py[0].X, py[0].Y, py[1].X, py[1].Y);
                        }
                        else{
                            g.DrawLine(InterPen, poly[0].X, poly[0].Y, poly[1].X, poly[1].Y);                            
                        }
                    }
                    else
                    {
                        g.FillPolygon(BrushInter, poly);//输出轮廓内部填充
                    }
                }
                else 
                {
                    if (poly.Length < 3)
                    {
                        if (poly.Length == 1)
                        {
                            PointF[] py = new PointF[2];
                            py[0] = poly[0]; py[1] = poly[0];
                            g.DrawLine(OneLine, py[0].X, py[0].Y, py[1].X, py[1].Y);
                        }
                        else { g.DrawLine(OneLine, poly[0].X, poly[0].Y, poly[1].X, poly[1].Y); }
                    }
                    else
                    {
                        g.DrawPolygon(OneLine, poly);//输出轮廓线
                    }
                }
            }

        }
        public static void SaveABigTiff(Graphics g, Bitmap outputBMP,int layerID, string FilePath)//————输出TIFF图形
        {
            //int num = Convert.ToInt16( layer.z / LayerThickness);
            outputBMP.Save(FilePath+Convert.ToString(layerID) +".tiff", ImageFormat.Tiff);//————保存到TIFF文件
            g.Dispose();//————————————————————————释放画笔
            outputBMP.Dispose();//————————————————————————释放bmp文件
        }

        public static void SortClI(CLI cli)//CLI轮廓线分类—————————————————————————————全部的排序
        {
            for (int i=0;i<cli.LayerNumber;i++)
            {
                SortLayer(cli.LayerLine[i]);
            }
        }

        public static void SortLayer(Layer layer)//—————————————————————————————————层线条排序
        {
            int n = layer.LineList.Count;//层layer的总线数
            for (int i=0;i<n;i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (GetArea(layer.LineList[i].Lines[0]) < GetArea(layer.LineList[j].Lines[0]))
                    {
                        var poly = layer.LineList[i];//冒泡法排序

                        layer.LineList[i] = layer.LineList[j];//冒泡法排序

                        layer.LineList[j] = poly;//冒泡法排序
                    }
                }
            }
        }

       public static double GetArea(PointF[] points)//面积，有正负————————————————————————————求面积
        {
            int point_num = points.Length;
            if (point_num < 3) { return 0.0; }
            else
            {
                double s = points[0].Y * (points[point_num - 1].X- points[1].X);
                for (int i = 1; i < points.Length; i++)
                    s += points[i].Y * (points[i - 1].X - points[(i + 1) % point_num].X);//———————————————计算面积：多边形面积计算公式
                double S= -s / 2.0;
                if (S < 0)
                { S= -S; }
                return S;//返回面积S
            }
        }
        //自动生成tiff,可及时停止。
        //主要生成TIFF在规定的路径下
        public static void OutPutTiff(CLI cli,string FilePath)//—————————————————————————输出TIFF图形
        {
            SortLinePosition(cli);
            SortClI(cli);

            double x, y;
            x = cli.Dimension[1].x - cli.Dimension[0].x;
            y = cli.Dimension[1].y - cli.Dimension[0].y;

            //for (int i = 0; i < 10; i++) 
            for (int i=0;i<cli.LayerNumber;i++)
            {
                if (StopOut == false)//——————————————————————————————————————输出停止标志位
                { SetTiff(cli.LayerLine[i], cli.LayerThickness, x+1, y+1, FilePath); }
                else
                {
                    StartLayer = i;
                    break;
                }
            }
            StopOut = false;//停止位清楚
        }
        //从特定层开始生成Tiff
        public static void OutPutTiff(CLI cli, string FilePath,int StartLayer)//——————————————————从指定层输出TIFF图形
        {
            SortLinePosition(cli);
            SortClI(cli);
            StopOut = false;//停止位清楚
            double x, y;
            x = cli.Dimension[1].x - cli.Dimension[0].x;
            y = cli.Dimension[1].y - cli.Dimension[0].y;
            if (StartLayer<cli.LayerNumber)
            { 
                for (int i = StartLayer; i < cli.LayerNumber; i++)
                {  
                    if (StopOut == false)
                    { SetTiff(cli.LayerLine[i], cli.LayerThickness, x, y, FilePath); }
                    else 
                    {
                        StartLayer = i;
                        break; 
                    }
                }
            }
        }

        /**********************************以下为新增*********************************************************/
        /// <summary>
        /// 生成一个固定范围的图片:根据CLI生成TIFF
        /// </summary>
        /// <param name="cli"></param>
        /// <param name="FilePath"></param>
        /// <param name="StartLayer"></param>
        /// <param name="EndLayer"></param>
        public static void OutPutTiff(CLI cli, string FilePath, int StartLayer, int EndLayer)//从制定出输出tiff图形，到指定层结束
        {
            SortLinePosition(cli);
            SortClI(cli);
            StopOut = false;//停止位清楚
            double x, y;
            x = cli.Dimension[1].x - cli.Dimension[0].x;
            y = cli.Dimension[1].y - cli.Dimension[0].y;
            if (StartLayer < cli.LayerNumber && EndLayer < cli.LayerNumber)
            {
                for (int i = StartLayer; i <= EndLayer; i++)
                {
                    if (StopOut == false)
                    { SetTiff(cli.LayerLine[i], cli.LayerThickness, x+1, y+1, FilePath); }
                    else
                    {
                        StartLayer = i;
                        break;
                    }
                }
            }
        }
        /// <summary>
        /// 生成固定层的小图形
        /// </summary>
        /// <param name="cli"></param>
        /// <param name="FilePath"></param>
        /// <param name="i"></param>
        public static void GetOneLayerTiff(CLI cli, string FilePath, int i)//生成指定层的tiff
        {
            SortLinePosition(cli);
            SortClI(cli);
            double x, y;
            x = cli.Dimension[1].x - cli.Dimension[0].x;
            y = cli.Dimension[1].y - cli.Dimension[0].y;
            if (StartLayer < cli.LayerNumber)
            {
                SetTiff(cli.LayerLine[i], cli.LayerThickness, x, y, FilePath);//存放到FilePATH
            }
        }
        /// <summary>
        /// 生成固定层的大图形
        /// </summary>
        /// <param name="cli"></param>
        /// <param name="FilePath"></param>
        /// <param name="layerID"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="g"></param>
        /// <param name="OuterPen"></param>
        /// <param name="BrushOuter"></param>
        /// <param name="InterPen"></param>
        /// <param name="BrushInter"></param>
        /// <param name="OneLine"></param>
        public static void GetOneLayerTiff(CLI cli, string FilePath, int layerID, float x,float y,Graphics g,
            Pen OuterPen, Brush BrushOuter, Pen InterPen, Brush BrushInter, Pen OneLine)//生成指定层的tiff
        {
            SortLinePosition(cli);
            SortClI(cli);
            //double x, y;
            //x = cli.Dimension[1].x - cli.Dimension[0].x;
            //y = cli.Dimension[1].y - cli.Dimension[0].y;
            if (StartLayer < cli.LayerNumber)
            {
                SetABigTiff(cli.LayerLine[layerID], FilePath, layerID, x,y, cli.LayerThickness,g,OuterPen,BrushOuter,InterPen,BrushInter,OneLine);//输出1层的图片
            }
        }

        public static void SortLinePosition(CLI cli)
        {
            double X = (0.01 - cli.Dimension[0].x);
            double Y = (0.01 - cli.Dimension[0].y);
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
        }
    }

}
