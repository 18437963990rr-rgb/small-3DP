using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BinderJetting
{
    class CLISubAeraAlgorithm//20210112新增：处理数据，实现分区弱连接
    {
        //补充绘制策略：总体策略就是编辑Pic,生成满足需要的WeakLine的图形，本质是一个绘制策略
        public void DrawWeakLine(ref List<List<PointF>> pointFs, int layerindex, int ControlCode, float x, float y, float height, float width, float space, float areawidth, float displacement)
        {
            switch (ControlCode)
            {
                case 1:
                    CreateSquareDisplaceWeakLine(ref pointFs, layerindex, x, y, height, width, space, areawidth, displacement);//最基础的大零件打印策略

                    break;
                case 2:
                    break;
                case 3:
                    break;
                case 4:
                    break;
            }
        }
        /// <summary>
        /// 补充绘制策略1：最基础的大零件打印策略//方形错开弱连接线
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="height"></param>
        /// <param name="weight"></param>
        private void CreateSquareDisplaceWeakLine(ref List<List<PointF>> pointFs, int layerindex, float x, float y,float height,float width, float space,float weakareawidth, float displacement)//方形错开弱连接线
        {
            //List<List<PointF>> pointFs = new List<List<PointF>>();//存储1层的所有弱连接区域，每个弱连接区域以List<PointF>形式存储；
            if (layerindex % 2 == 0)//是偶数层
            {
                for (int i=1; i< width / space+2;i++)//添加纵线
                {
                    if ((x + i * space + weakareawidth / 2)< x + width)
                    {
                        List<PointF> TempPointF = new List<PointF>();
                        //(1)计算弱连接区域：
                        TempPointF.Add(new PointF(x + i * space - weakareawidth / 2, y));
                        TempPointF.Add(new PointF(x + i * space + weakareawidth / 2, y));
                        TempPointF.Add(new PointF(x + i * space + weakareawidth / 2, y + height));
                        TempPointF.Add(new PointF(x + i * space - weakareawidth / 2, y + height));

                        pointFs.Add(TempPointF);
                        //(2)添加弱连接区域到deviceContext：
                        //转移到5-SharpControl中实现

                        //PointF[] TempPointF11 = TempPointF.ToArray();//绘制的弱连接区域
                        //RawVector2[] DrawPointF = null;
                        //PointF2Vector(TempPointF11, ref DrawPointF, deltax, deltay);
                        //PathGeometry geometry = new PathGeometry(deviceContext.Factory);
                        //var geometrySink = geometry.Open();
                        //geometrySink.BeginFigure(DrawPointF[0], FigureBegin.Filled);
                        //geometrySink.AddLines(DrawPointF);
                        //geometrySink.EndFigure(FigureEnd.Closed);//FigureEnd.Closed和FigureEnd.Open
                        //geometrySink.Close();           
                        //deviceContext.FillGeometry(geometry, WeakLineBrush);//添加一部分零件细节：核心轻量级：较接近与MetaFile的功能
                    }
                }
#if true//20210122新增：Y向分区不生效
                for (int i = 1; i < height / space+2; i++)//添加横线
                {
                    if ((y + i * space + weakareawidth / 2) < y + height)
                    {
                        List<PointF> TempPointF2 = new List<PointF>();
                        //(1)计算弱连接区域：
                        TempPointF2.Add(new PointF(x        , y + i * space - weakareawidth / 2));
                        TempPointF2.Add(new PointF(x + width, y + i * space - weakareawidth / 2));
                        TempPointF2.Add(new PointF(x + width, y + i * space + weakareawidth / 2));
                        TempPointF2.Add(new PointF(x        , y + i * space + weakareawidth / 2));

                        pointFs.Add(TempPointF2);
                        //(2)添加弱连接区域到deviceContext：
                        //转移到5-SharpControl中实现
                    }
                }
#endif
            }
            else//是奇数层
            {
                for (int i = 1; i < width / space+2; i++)//添加纵线
                {
                    if((x + (i - 0.5f) * space + weakareawidth / 2) < x + width)
                    {
                        List<PointF> TempPointF = new List<PointF>();
                        //(1)计算弱连接区域：
                        TempPointF.Add(new PointF(x + (i - 0.5f) * space - weakareawidth / 2, y));
                        TempPointF.Add(new PointF(x + (i - 0.5f) * space + weakareawidth / 2, y));
                        TempPointF.Add(new PointF(x + (i - 0.5f) * space + weakareawidth / 2, y + height));
                        TempPointF.Add(new PointF(x + (i - 0.5f) * space - weakareawidth / 2, y + height));

                        pointFs.Add(TempPointF);
                        //(2)添加弱连接区域到deviceContext：
                        //转移到5-SharpControl中实现
                    }

                }
#if true//20210122新增：Y向分区不生效
                for (int i = 1; i < height / space+2; i++)//添加横线
                {
                    if ((y + (i - 0.5f) * space + weakareawidth / 2) < y + height)
                    {
                        List<PointF> TempPointF2 = new List<PointF>();
                        //(1)计算弱连接区域：
                        TempPointF2.Add(new PointF(x        , y + (i - 0.5f) * space - weakareawidth / 2));
                        TempPointF2.Add(new PointF(x + width, y + (i - 0.5f) * space - weakareawidth / 2));
                        TempPointF2.Add(new PointF(x + width, y + (i - 0.5f) * space + weakareawidth / 2));
                        TempPointF2.Add(new PointF(x        , y + (i - 0.5f) * space + weakareawidth / 2));

                        pointFs.Add(TempPointF2);
                        //(2)添加弱连接区域到deviceContext：
                        //转移到5-SharpControl中实现
                    }
                }
#endif
            }

            //for(int i=0;i< pointFs.Count(); i++)
            //{
            //    //(2)添加弱连接区域到deviceContext：
            //    PointF[] TempPointF = pointFs[i].ToArray();//绘制的弱连接区域
            //    RawVector2[] DrawPointF = null;
            //    PointF2Vector(TempPointF, ref DrawPointF, deltax, deltay);//进行坐标系变换
            //    PathGeometry geometry = new PathGeometry(deviceContext.Factory);
            //    var geometrySink = geometry.Open();
            //    geometrySink.BeginFigure(DrawPointF[0], FigureBegin.Filled);
            //    geometrySink.AddLines(DrawPointF);
            //    geometrySink.EndFigure(FigureEnd.Closed);//FigureEnd.Closed和FigureEnd.Open
            //    geometrySink.Close();
            //    deviceContext.FillGeometry(geometry, WeakLineBrush);//添加一部分零件细节：核心轻量级：较接近与MetaFile的功能
            //}

        }

    }
}
