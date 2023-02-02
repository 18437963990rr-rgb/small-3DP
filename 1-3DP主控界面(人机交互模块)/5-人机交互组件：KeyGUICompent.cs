using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using _3DP_KeyLibrary;
using System.Collections;
using Composation;

namespace BinderJetting
{
    class _3DP_GUI组件 : IGui
    {
        //20200225新增
        public Point s_min, s_max;//——————选中图片区域左上角和右下角的点的坐标//20200225新增

        //鼠标按下坐标、缩放数
        public Point MouseDownP = new Point();//鼠标按下时坐标      
        public int zoom = 1; //初始放大倍数//————修改成double，看是否可以public int zoom = 0;//差点被你搞死
        public int pixel2MM = 2;//初始缩放比例

        //20200225新增
        public Point r_d_point;//20200225新增


        //记录图片位置
        public int num_p = 0;//变量，显示计数变量，第几张图片
                             //public ArrayList PX = new ArrayList();//像素坐标X
                             //public ArrayList PY = new ArrayList();//像素坐标Y
                             //public ArrayList px = new ArrayList();//坐标轴坐标x
                             //public ArrayList py = new ArrayList();//坐标轴坐标y
                             //public ArrayList PH = new ArrayList();//图片的高度
                             //public ArrayList PW = new ArrayList();//图片的宽度
                             //public ArrayList LAYER = new ArrayList();//图片所在层数
                             //public ArrayList FLAG = new ArrayList();//图片是否选中的标志位
        public List<int> PX = new List<int>();//像素坐标X
        public List<int> PY = new List<int>();//像素坐标Y
        public List<int> px = new List<int>();//坐标轴坐标x
        public List<int> py = new List<int>();//坐标轴坐标y
        public List<int> PH = new List<int>();//图片的高度
        public List<int> PW = new List<int>();//图片的宽度
        public List<int> LAYER = new List<int>();//图片所在层数
        public List<int> FLAG = new List<int>();//图片是否选中的标志位

        public List<int> StlFlag = new List<int>();//多个STL的1次初始位置输入控制（SHIBIAO新增），初始调用到对应索引的时候，对应索引设置为1。当下次显示时，对应索引再重置为0；
        public bool ResetFlag = true;//刷新输入STL零件的标志位

        public int CLInum = 0;//常量：the num of the TiffNum in PATH，it can be modified in the MAINFORM, and it must be public.
        public List<JobItem> tempJobItems = new List<JobItem>();//全部的job参数，包含了所有STL位置、参数
        static string RESOURSE;//图片的来源路径,此处暂时先不改！！！
        public List<string> TIFFPathCopy = new List<string>();//record the TIFF FILE

        public int sr_Flag = 0;//是否选中图片标志位————————————修改为public
        public Point firstpoint;//修改为public
        public Point secondpoint;//修改为public
        
        //20200223新增:多线程绘制图像数据
        //20200223新增:多线程绘制图像数据
        public Point Firstpoint;
        public Point Secondpoint;
        //20200223新增:多线程绘制图像数据
        //20200223新增:多线程绘制图像数据

        public Bitmap bt = new Bitmap(3000, 3000);//画在这张图片上面————————————————————————进行了修改！！！！！
        Image img;
        public float img_x = 620, img_y = 20 + 700 / 2, MouseDown_x, MouseDown_y,
            MouseUp_x = 0, MouseUp_y = 0, MouseWheel_x = 0, MouseWheel_y = 0;//————————————————修改为public
        public int img_w, img_h, select_Flag = 0;

        //20200223新增:多线程绘制图像数据
        //20200223新增:多线程绘制图像数据
        //20200223新增:多线程绘制图像数据
        public int mov_F = 0;
        public Point cal_MouseDownP = new Point();//鼠标按下时坐标      
        public int cal_zoom = 1; //初始放大倍数//————修改成double，看是否可以public int zoom = 0;//差点被你搞死
        public int cal_pixel2MM = 2;//初始缩放比例

        //记录图片位置
        public int cal_num_p = 0;//变量，显示计数变量，第几张图片
        public List<int> cal_PX = new List<int>();//像素坐标X
        public List<int> cal_PY = new List<int>();//像素坐标Y
        public List<int> cal_px = new List<int>();//坐标轴坐标x
        public List<int> cal_py = new List<int>();//坐标轴坐标y
        public List<int> cal_PH = new List<int>();//图片的高度
        public List<int> cal_PW = new List<int>();//图片的宽度
        public List<int> cal_LAYER = new List<int>();//图片所在层数
        public List<int> cal_FLAG = new List<int>();//图片是否选中的标志位
        public List<string> cal_RESourse = new List<string>();
        public List<int> cal_StlFlag = new List<int>();//多个STL的1次初始位置输入控制（SHIBIAO新增），初始调用到对应索引的时候，对应索引设置为1。当下次显示时，对应索引再重置为0；

        public bool cal_ResetFlag = true;//刷新输入STL零件的标志位

        public int cal_CLInum = 0;//常量：the num of the TiffNum in PATH，it can be modified in the MAINFORM, and it must be public.

        public List<JobItem> cal_tempJobItems = new List<JobItem>();//全部的job参数，包含了所有STL位置、参数

        static string cal_RESOURSE;//图片的来源路径,此处暂时先不改！！！

        public List<string> cal_TIFFPathCopy = new List<string>();//record the TIFF FILE

        public int cal_sr_Flag = 0;//是否选中图片标志位————————————修改为public
        public Point cal_firstpoint;//修改为public
        public Point cal_Firstpoint;
        public Point cal_secondpoint;//修改为public
        public Point cal_Secondpoint;


        public Bitmap cal_bt = new Bitmap(3000, 3000);//画在这张图片上面————————————————————————进行了修改！！！！！
        Image cal_img;

        public float cal_img_x = 620, cal_img_y = 20 + 700 / 2, cal_MouseDown_x, cal_MouseDown_y,
            cal_MouseUp_x = 0, cal_MouseUp_y = 0, cal_MouseWheel_x = 0, cal_MouseWheel_y = 0;//————————————————修改为public
        public int cal_img_w, cal_img_h, cal_select_Flag = 0;
        public int cal_mov_F = 0;
        //20200223新增:多线程绘制图像数据
        //20200223新增:多线程绘制图像数据
        //20200223新增:多线程绘制图像数据

        //*****************************************************接口实现*******************************************************/
        public bool Login(string UserName, string PassWord)
        {
            throw new NotImplementedException();
        }

        public void 显示2D加工截面()
        {
            throw new NotImplementedException();
        }
  
        public void 显示3D加工预览()
        {
            throw new NotImplementedException();
        }   
        /***************************************BEGIN:capsulated into a function of AddImage2LINK*************************************************/
        //x and y positon can be negtive and positive.//xsize and ysize are the size of the parent panel
        //this part aims to register the aim picture to the stlFlag and the move the aim pictures into the LINK
        //add image to link from list<CLItype> STLlist=new list<CLItype>();
        public Picture AddImage2lINK(int STLid,int x,int y,string imagePath,int xsize,int ysize)//p1/p2/p3/p4/p5置换为flag，flag是第一次显示的标志，输入的参数应该是StlFlag[i],表示第i项的状态。
        {
            Picture pic = null;           
            if (StlFlag[STLid]==0)//初始化标志位：如果count小于STLid就说明还没有初始化
            {
                PX.Add(0);
                PY.Add(0);
                px.Add(x);//初始值是（0，0），自定义坐标（以坐标系原点为基准）
                py.Add(y);//初始值是（0，0），自定义坐标（以坐标系原点为基准）
                PH.Add(0);
                PW.Add(0);
                LAYER.Add(0);
                FLAG.Add(0);

                StlFlag[STLid]=1;//第1次注册图片，在STLid位置新增1项int标志位，并将位置1

                PX[STLid] = (int)px[STLid] * pixel2MM + (xsize / 2 + 20);
                PY[STLid] = (int)py[STLid] * pixel2MM + (ysize / 2 + 20);
            }
            //in pic, mainly contain the information of path string
            pic = new Picture(imagePath, (int)PX[STLid], (int)PY[STLid],(int)PH[STLid], (int)PW[STLid], (int)LAYER[STLid], (int)FLAG[STLid]);
            return pic;
        }
        /***************************************END:capsulated into a function of AddImage2LINK*************************************************/

        /************************************************************************************/
        //public List<JobItem> tempJobItems = new List<JobItem>();
        //Initiate the local PIC LOCATION and Download the JOBITEM
        public void RegisterComposPic(List<JobItem> jobItems)//Initiate the local PIC LOCATION and Download the JOBITEM
        {
            //进入之后，下面代码才可以正确运行
            TIFFPathCopy.Clear();//情况CLI生成的TIFF路径
            PX.Clear();
            PY.Clear();
            px.Clear();
            py.Clear();
            PH.Clear();
            PW.Clear();
            LAYER.Clear();
            FLAG.Clear();
            CLInum = jobItems.Count;//清空计数：
            tempJobItems = jobItems;//translate the jobItems in the Composation Space to the local jobItems
            ResetFlag = true; //when the import CLI file was changed, the resetFlag must be set true immediately
        }

        //public bool ResetFlag = true;
        //public List<string> TIFFPathCopy = new List<string>();//record the TIFF FILE
        //public int CLInum = 0;//the num of the TiffNum in PATH————it can be modified in the MAINFORM, and it must be public.       
        public void LINK(Graphics g, int xsize, int ysize)//图片的链表————————————————————————————————本方法，可以修改成循环形式，否则代码量太大
        {
            //DrawImg();//重绘背景————————————————————————————此处进行了改动
            DrawImageBase(g, xsize, ysize);//重绘基板

            LinkedList<Picture> linkListPicture = new LinkedList<Picture>();
            Picture pic = null;
            num_p = 0;//the temp conter to register the TIFF to the buffer picture.

            /*************************NEW CHANGED**********************************/
            // This is VERY important
            if (ResetFlag == true)//CLInum = 5;//the num of the TiffNum in PATH
            {
                StlFlag.Clear();
                StlFlag = new List<int>(new int[CLInum]);//初始化指定大小的数组。               
                ResetFlag = false;
            }//重新复位所有的标志位：未来，第2次添加或者删除一些图片时，用到

            for (num_p = 0; num_p < tempJobItems.Count; num_p++)//5为导入的总的STL的个数。
            {
                //We must have three parameter to define the positon in buffer picture.(xposition, yposition, and path)
                //linkListPicture.AddLast(AddImage2lINK(num_p, -40, -40, $@"C:\Users\SummerGhost\Documents\Visual Studio 2017\Projects\LaserAdd_3DP_Software
                //\1-3DP主控界面(人机交互模块)\bin\Debug\TIFF输出文件\测试-1\0.tiff", xsize, ysize));//添加图片       
                linkListPicture.AddLast(AddImage2lINK(num_p, (int)tempJobItems[num_p].position.X-210,
                     (int)tempJobItems[num_p].position.Y-175, tempJobItems[num_p].Path, xsize, ysize));//添加图片       
            }
            num_p = 0;
            if (CLInum!=0)//如果是注册了零件，就去显示零件到buffer image
            {
                LinkedListNode<Picture> linkNodePicture = linkListPicture.First;
                linkNodePicture.Value.Picture_resourse();//获取路径//20200223
                //RESOURSE = linkNodePicture.Value.Picture_resourse();//获取路径//20200223
                Picture_Drawing(g, xsize, ysize);//绘制第一张图片
                while (linkNodePicture.Next != null)//依次绘制图片
                {

                    linkNodePicture = linkNodePicture.Next;
                    linkNodePicture.Value.Picture_resourse();
                    //RESOURSE = linkNodePicture.Value.Picture_resourse();//获取路径//20200223
                    num_p++;

                    Picture_Drawing(g, xsize, ysize);
                }
            }
            else { }

            g.ResetTransform();//变换的坐标回复原来的位置,共用1个g进行操作，必须要进行：世界变换矩阵重置为单位矩阵！！！！！
            DrawImgBG(g, xsize, ysize);//标尺背景
            DrawImgCoord(g, xsize, ysize);//标尺
        }

        public class Picture//图片的定义——图片的相关参数——没有真正用到
        {
            public Picture()
            { }
            public Picture(string resourse, int p_x, int p_y, int p_w, int p_h, int layer, int p_s)
            {
                this.Resourse = resourse;
                this.P_x = p_x;
                this.Py = p_y;
                this.P_w = p_w;
                this.P_h = p_h;
                this.Layer = layer;
                this.P_s = p_s;
            }
            public string Resourse { get; set; }
            public int P_x { get; set; }
            public int Py { get; set; }
            public int P_w { get; set; }
            public int P_h { get; set; }
            public int Layer { get; set; }
            public int P_s { get; set; }
            public void Picture_resourse()
            {
                RESOURSE = this.Resourse;//RESOURCE不必要修改
            }
        }

        /***************************************上面是整个图片的定义*****************************************/
        //public int pixel2MM = 2;
        public void DrawImageBase(Graphics g,int xsize,int ysize)//缩放重绘
        {
#if false//20200518新建：
            g.PageScale = 1;
            g.PageUnit = System.Drawing.GraphicsUnit.Millimeter;

#endif

            g.FillRectangle(new SolidBrush(Color.WhiteSmoke), 0, 0, xsize, ysize);

            //设置背景色渐变——————————新增的界面的背景	
            System.Drawing.Drawing2D.GraphicsPath gp = new System.Drawing.Drawing2D.GraphicsPath();
            Rectangle rec = new Rectangle(20, 20, xsize, ysize);
            gp.AddRectangle(rec);
            Color[] surroundColor = new Color[] { Color.FromArgb(150, Color.LightBlue) };
            System.Drawing.Drawing2D.PathGradientBrush pb = new System.Drawing.Drawing2D.PathGradientBrush(gp);
            pb.CenterColor = Color.White;
            pb.SurroundColors = surroundColor;
            g.FillPath(pb, gp);

            Pen p = new Pen(Color.Black, 1);//定义了一个黑色,宽度为1的画笔
            Pen p01 = new Pen(Color.White, 1);
            Rectangle rect = new Rectangle(0, 0, xsize+20,ysize+20);

            g.DrawRectangle(p, rect);

            SolidBrush b1 = new SolidBrush(Color.Blue);
            SolidBrush b2 = new SolidBrush(Color.White);

            p.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
            rect.Height = 350 * pixel2MM * zoom;
            rect.Width = 420 * pixel2MM * zoom;

            p.Color = Color.Green;
            p.Width = (float)2;//基板外框线————没什么用处
  
            rect.Location = new Point((xsize / 2 + 20 - 210 * pixel2MM - MouseDownP.X) * zoom
                + MouseDownP.X, (ysize / 2 + 20 - 175 * pixel2MM - MouseDownP.Y) * zoom + MouseDownP.Y);

            g.FillRectangle(new SolidBrush(Color.LightYellow), rect);//重绘基板背景色
            //g.FillRectangle(new SolidBrush(Color.Wheat), rect);//重绘基板背景色
            //g.FillRectangle(new SolidBrush(Color.Red), rect);//重绘基板背景色
            g.DrawRectangle(p, rect);
            p.Color = Color.Black;

            p.Width = 1;//基板外框线的宽度（画笔的宽度）
            p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            for (int i = -210; i <= 210; i = i + 10)
            {
                if (i % 10 == 0)
                {
                    g.DrawLine(p, new Point((xsize / 2 + 20 + i * pixel2MM - MouseDownP.X) * zoom
                        + MouseDownP.X, (ysize / 2 + 20 - 175 * pixel2MM - MouseDownP.Y) * zoom + MouseDownP.Y),
                        new Point((xsize / 2 + 20 + i * pixel2MM - MouseDownP.X) * zoom + MouseDownP.X,
                        (ysize / 2 + 20 + 175 * pixel2MM - MouseDownP.Y) * zoom + MouseDownP.Y));
                }
            }
            for (int i = -170; i <= 170; i = i + 10)
            {
                if (i % 10 == 0)
                {
                    g.DrawLine(p, new Point((xsize / 2 + 20 - 210 * pixel2MM - MouseDownP.X) * zoom
                        + MouseDownP.X, (ysize / 2 + 20 + i * pixel2MM - MouseDownP.Y) * zoom + MouseDownP.Y),
                        new Point((xsize / 2 + 20 + 210 * pixel2MM - MouseDownP.X) * zoom + MouseDownP.X,
                        (ysize / 2 + 20 + i * pixel2MM - MouseDownP.Y) * zoom + MouseDownP.Y));
                }
            }
            p.DashPattern = new float[] { 3, 3 };//设置短划线和空白部分的数组  5为虚线长度，1为虚线间距
       
            p.Width =Convert.ToSingle(2);//中轴线（虚线）
            p.Color = Color.BlueViolet;

            g.DrawLine(p, new Point(0, (ysize / 2 + 20 + 0 * pixel2MM - MouseDownP.Y) * zoom + MouseDownP.Y),
                new Point(xsize+20, (ysize / 2 + 20 + 0 * pixel2MM - MouseDownP.Y) * zoom + MouseDownP.Y));
            g.DrawLine(p, new Point((xsize / 2 + 20 + 0 * pixel2MM - MouseDownP.X) * zoom + MouseDownP.X, 0),
                new Point((xsize / 2 + 20 + 0 * pixel2MM - MouseDownP.X) * zoom + MouseDownP.X, ysize+20));
            // }
            p.Width = (float)1;
        }
        public void DrawImgBG(Graphics g, int xsize, int ysize)//绘制背景
        {
            Pen p01 = new Pen(Color.White, 1);
            Rectangle rect = new Rectangle(0, 0, xsize+20, ysize);
            //g.DrawRectangle(p, rect);//定义了一个黑色,宽度为1的画笔————————————去掉没啥影响————绘制方框！！！！
            Rectangle rect01 = new Rectangle(0, 0, 20, ysize);
            Rectangle rect02 = new Rectangle(0, 0, xsize+20, 20);
            SolidBrush b1 = new SolidBrush(Color.Blue);
            g.FillRectangle(b1, rect01);
            g.FillRectangle(b1, rect02);
            
        }

        //绘制坐标系的标尺——这一步是关键
        public void DrawImgCoord(Graphics g, int xsize, int ysize)//绘制坐标系的标尺
        {            
            //陈锌修改
            string[] zuobiao_p = new string[31] { "0", "20", "40", "60", "80", "100", "120", "140", "160",
                "180", "200", "220", "240", "260", "280", "300", "320", "340", "360", "380", "400", "420",
                "440", "460", "480", "500", "520", "540", "560", "580", "600"};
            string[] zuobiao_n = new string[31]  { "0", "-20", "-40", "-60", "-80", "-100", "-120", "-140", "-160",
                "-180", "-200", "-220", "-240", "-260", "-280", "-300", "-320", "-340", "-360", "-380", "-400", "-420",
                "-440", "-460", "-480", "-500", "-520", "-540", "-560", "-580", "-600"};

            //Pen p = new Pen(Color.Black, 1);//定义了一个黑色,宽度为1的画笔————————————去掉没啥影响————绘制方框！！！！
            Pen p01 = new Pen(Color.White, 1);
            //g.DrawRectangle(p, rect);//定义了一个黑色,宽度为1的画笔————————————去掉没啥影响————绘制方框！！！！

            SolidBrush b1 = new SolidBrush(Color.Blue);
            SolidBrush b2 = new SolidBrush(Color.White);

            //(1)绘制X轴标尺——正半轴           
            //陈锌修改
            for (int i0 = (xsize / 2 + 20 + 0 * pixel2MM - MouseDownP.X) * zoom + MouseDownP.X, i = i0; i <= xsize; i = i + pixel2MM * zoom)//X轴正坐标
            {
                if ((i - i0) % (pixel2MM*20 * zoom) == 0)
                {
                    g.DrawLine(p01, i, 15, i, 20);
                    g.DrawString(zuobiao_p[(i - i0) / (pixel2MM * 20 * zoom)], new Font("宋体", 10), b2, new Point(i-10, 0));//其中的new Point(i-20, 0)对于调整string的位置比较关键
                }
                else if ((i - i0) % (pixel2MM * zoom) == 0)
                {
                    g.DrawLine(p01, i, 18, i, 20);
                }
            }

            ////(2)绘制X轴标尺——负半轴
            for (int i0 = (xsize / 2 + 20 + 0 * pixel2MM - MouseDownP.X) * zoom + MouseDownP.X, i = i0; i >= 20; i = i - pixel2MM * zoom)//X轴负坐标
            {
                if ((i - i0) % (pixel2MM * 20 * zoom) == 0)
                {
                    g.DrawLine(p01, i, 15, i, 20);
                    g.DrawString(zuobiao_n[(i0 - i) / (pixel2MM * 20 * zoom)], new Font("宋体", 10), b2, new Point(i-10, 0));
                }
                else if ((i - i0) % (pixel2MM * zoom) == 0)
                {
                    g.DrawLine(p01, i, 18, i, 20);
                }
            }

            //(3)绘制Y轴标尺——正半轴————————都用到了字符显示方向旋转——————！！！！
            for (int i0 = (ysize / 2 + 20 + 0 * pixel2MM - MouseDownP.Y) * zoom + MouseDownP.Y, i = i0; i <= ysize; i = i + pixel2MM * zoom)//Y轴正坐标
            {
                if ((i - i0) % (pixel2MM * 20 * zoom) == 0)
                {
                    StringFormat format1 = new StringFormat();
                    //指定字符串的水平对齐方式
                    format1.Alignment = StringAlignment.Far;
                    //表示字符串的垂直对齐方式
                    format1.LineAlignment = StringAlignment.Center;
                    //旋转角度和平移
                    System.Drawing.Drawing2D.Matrix mtxRotate = g.Transform;

                    mtxRotate.RotateAt(90, new PointF(5, 10));//————————————————旋转了字符显示方向——————其他的用的是
                    g.Transform = mtxRotate;
                    if ((i - i0) / (pixel2MM * 20 * zoom) >= 0 && (i - i0) / (pixel2MM * 20 * zoom) <= 30)
                        g.DrawString(zuobiao_p[(i - i0) / (pixel2MM * 20 * zoom)], new Font("宋体", 10), b2, new Point(i-15, 0));//写好字体——————写文字
                    g.ResetTransform();//回复画笔的坐标轴——————坐标轴方向复位

                    g.DrawLine(p01, 15, i, 20, i);

                }
                else if ((i - i0) % (pixel2MM * zoom) == 0)
                {
                    g.DrawLine(p01, 18, i, 20, i);
                }
            }

            ////(4)绘制Y轴标尺——负半轴————————都用到了字符显示方向旋转——————！！！
            for (int i0 = (ysize / 2 + 20 + 0 * pixel2MM - MouseDownP.Y) * zoom + MouseDownP.Y, i = i0; i >= 20; i = i - pixel2MM * zoom)//Y轴负坐标
            {
                if ((i - i0) % (pixel2MM * 20 * zoom) == 0)
                {
                    StringFormat format1 = new StringFormat();
                    //指定字符串的水平对齐方式
                    format1.Alignment = StringAlignment.Far;
                    //表示字符串的垂直对齐方式
                    format1.LineAlignment = StringAlignment.Center;
                    //旋转角度和平移
                    System.Drawing.Drawing2D.Matrix mtxRotate = g.Transform;

                    mtxRotate.RotateAt(90, new PointF(5, 10));
                    g.Transform = mtxRotate;

                    g.DrawString(zuobiao_n[(i0 - i) / (pixel2MM * 20 * zoom)], new Font("宋体", 10), b2, new Point(i-15, 0));
                    g.ResetTransform();

                    g.DrawLine(p01, 15, i, 20, i);
                }
                else if ((i - i0) % (pixel2MM * zoom) == 0)
                {
                    g.DrawLine(p01, 18, i, 20, i);
                }
            }
        }

        public void First_Paint(Graphics g,int xsize,int ysize)//初始界面绘图方法
        {
            //Graphics g = this.CreateGraphics(); //创建画板,这里的画板是由Form提供的.　　
            string[] zuobiaox = new string[31] { "-120", "-110", "-100", "-90", "-80", "-70", "-60",
                "-50", "-40", "-30", "-20", "-10", "0", "10", "20", "30", "40", "50", "60", "70",
                "80", "90", "100", "110", "120", "250", "260", "270", "280", "290", "300" };
            string[] zuobiaoy = new string[31] { "-70", "-60", "-50", "-40", "-30", "-20", "-10",
                "0", "10", "20", "30", "40", "50", "60", "70", "80", "90", "170", "180", "190",
                "200", "210", "220", "230", "240", "250", "260", "270", "280", "290", "300" };

            //设置背景色渐变——————————新增的界面的背景	

            Pen p = new Pen(Color.Black, 1);//定义了一个黑色,宽度为1的画笔
            Pen p01 = new Pen(Color.White, 1);
            Rectangle rect = new Rectangle(0, 0, 1220, 720);
            g.DrawRectangle(p, rect);
            Rectangle rect01 = new Rectangle(0, 0, 20, 720);
            Rectangle rect02 = new Rectangle(0, 0, 1220, 20);
            SolidBrush b1 = new SolidBrush(Color.Blue);
            SolidBrush b2 = new SolidBrush(Color.White);
            g.FillRectangle(b1, rect01);
            g.FillRectangle(b1, rect02);

            for (int i = 0; i <= 1220; i = i + 5)
            {
                if ((i - 20) % (5*10) == 0)
                {
                    g.DrawLine(p01, i, 15, i, 20);
                    g.DrawString(zuobiaox[(i - 20) / (5*10)], new Font("宋体", 10), b2, new Point(i - 10, 0));
                }
                else if (i % 5 == 0 && i >= 20)
                {
                    g.DrawLine(p01, i, 18, i, 20);
                }
            }
            for (int i = 0; i <= 720; i = i + 5)
            {
                if ((i - 20) % (5*10) == 0)
                {

                    StringFormat format1 = new StringFormat();
                    //指定字符串的水平对齐方式
                    format1.Alignment = StringAlignment.Far;
                    //表示字符串的垂直对齐方式
                    format1.LineAlignment = StringAlignment.Center;
                    //旋转角度和平移
                    System.Drawing.Drawing2D.Matrix mtxRotate = g.Transform;

                    mtxRotate.RotateAt(90, new PointF(5, 10));
                    g.Transform = mtxRotate;

                    g.DrawString(zuobiaoy[(i - 20) / 50], new Font("宋体", 10), b2, new Point(i - 10, 0));

                    g.ResetTransform();
                    g.DrawLine(p01, 15, i, 20, i);
                }
                else if (i % 5 == 0 && i >= 20)
                {
                    g.DrawLine(p01, 18, i, 20, i);
                }
            }

            p.DashPattern = new float[] { 3, 3 };//设置短划线和空白部分的数组  5为虚线长度，1为虚线间距
            g.DrawLine(p, new Point(1200 / 2 + 20, 20), new Point(1200 / 2 + 20, 720));
            g.DrawLine(p, new Point(20, 700 / 2 + 20), new Point(1220, 700 / 2 + 20));
            g.TranslateTransform(1200 / 2 + 20, 700 / 2 + 20);

            p.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
            rect.Height = 350 * (int)5;
            rect.Width = 420 * (int)5;
            rect.Location = new Point(-210 * (int)5, -175 * (int)5);
            g.DrawRectangle(p, rect);

            p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            for (int i = -210; i <= 210; i = i + 10)
            {
                if (i % 10 == 0)
                {
                    g.DrawLine(p, new Point((xsize / 2 + 20 + i * pixel2MM - MouseDownP.X) * zoom
                        + MouseDownP.X, (ysize / 2 + 20 - 175 * pixel2MM - MouseDownP.Y) * zoom + MouseDownP.Y),
                        new Point((xsize / 2 + 20 + i * pixel2MM - MouseDownP.X) * zoom + MouseDownP.X,
                        (ysize / 2 + 20 + 175 * pixel2MM - MouseDownP.Y) * zoom + MouseDownP.Y));
                }
            }
            for (int i = -170; i <= 170; i = i + 10)
            {
                if (i % 10 == 0)
                {
                    g.DrawLine(p, new Point((xsize / 2 + 20 - 210 * pixel2MM - MouseDownP.X) * zoom
                        + MouseDownP.X, (ysize / 2 + 20 + i * pixel2MM - MouseDownP.Y) * zoom + MouseDownP.Y),
                        new Point((xsize / 2 + 20 + 210 * pixel2MM - MouseDownP.X) * zoom + MouseDownP.X,
                        (ysize / 2 + 20 + i * pixel2MM - MouseDownP.Y) * zoom + MouseDownP.Y));
                }
            }
        }

        /************************************绘制图片的基本代码（绘制一张图片）************************************/
        //public float img_x = 620, img_y = 20 + 700 / 2, MouseDown_x, MouseDown_y,//————————————————修改为public
        //MouseUp_x = 0, MouseUp_y = 0, MouseWheel_x = 0, MouseWheel_y = 0;
        //public int img_w, img_h, select_Flag = 0;
        //public Bitmap bt=new Bitmap(3000,3000);//画在这张图片上面————————————————————————进行了修改！！！！！
        //Image img;
        public void SetBitmap(int xsize, int ysize)//设置画布的大小——————新增方法
        {
            bt.SetPixel(xsize, ysize, Color.FromArgb(0, 0, 0));//———————————进行了修改！！！！！
        }
        private void Picture_Drawing(Graphics g, int xsize, int ysize)//绘制图片
        {
            img = Image.FromFile(RESOURSE);

            img_w = img.Width;//————————————————————图片的宽度（其实没有yogndao）
            img_h = img.Height;//————————————————————图片的宽度（其实没有yogndao）
            img_y = (int)PY[num_p];
            img_x = (int)PX[num_p];

            //float DPI = img.HorizontalResolution / 120 / ((float)25.4*(float)5 / (float)120);//获取垂直分辨率和水平分辨率
            float DPI = img.HorizontalResolution / 120 / ((float)25.4 * (float)pixel2MM / (float)120);//获取垂直分辨率和水平分辨率

            //PH[num_p] = img.Height;//————————————存在bug（没考虑分辨率），世彪修改
            //PW[num_p] = img.Width;//————————————存在bug（没考虑分辨率），世彪修改
            PH[num_p] = (int)(img.Height / DPI);//现在用不到了————还是用到了
            PW[num_p] = (int)(img.Width / DPI);

            if ((int)FLAG[num_p] == 0)
            {

                g.DrawImage(img,(img_x- MouseDownP.X) * zoom+ MouseDownP.X,
                    (img_y - MouseDownP.Y ) * zoom + MouseDownP.Y , img.Width / DPI * zoom , img.Height / DPI * zoom );
            }
            else if ((int)FLAG[num_p] == 1)
            {
                Brush br = new SolidBrush(Color.FromArgb(50, Color.Blue));
                if (zoom != 1)
                {
                    g.DrawImage(img, (img_x - MouseDownP.X) * zoom + MouseDownP.X,
                   (img_y - MouseDownP.Y) * zoom + MouseDownP.Y, img.Width / DPI * zoom , img.Height / DPI * zoom );
                    Rectangle rb = new Rectangle((int)((img_x - MouseDownP.X) * zoom + MouseDownP.X),
                    (int)((img_y - MouseDownP.Y) * zoom + MouseDownP.Y), (int)((float)img_w/ DPI* zoom), (int)((float)img_h / DPI * zoom));//———存在bug（没考虑分辨率），世彪修改
                    g.FillRectangle(br, rb);//——————选中之后，表面覆盖的淡蓝色的选中状态                  
                }
                else
                {
                    g.DrawImage(img, (img_x - MouseDownP.X) * zoom + MouseDownP.X,
                    (img_y - MouseDownP.Y) * zoom + MouseDownP.Y, img.Width / DPI * zoom , img.Height / DPI * zoom) ;
                    //Rectangle rb = new Rectangle((int)img_x, (int)img_y, img_w, img_h);//———存在bug（没考虑分辨率），世彪修改
                    Rectangle rb = new Rectangle((int)((img_x - MouseDownP.X) * zoom + MouseDownP.X),
                    (int)((img_y - MouseDownP.Y) * zoom + MouseDownP.Y), (int)((float)img_w / DPI ), (int)((float)img_h / DPI ));//———存在bug（没考虑分辨率），世彪修改
                    g.FillRectangle(br, rb);
                }
            }
            g.ResetTransform();//变换的坐标回复原来的位置————————共用1个g进行操作，必须要进行：世界变换矩阵重置为单位矩阵——————！！！！！
            //DrawImgBG(g,xsize,ysize);//————————————————标尺背景
            //DrawImgCoord(g, xsize, ysize);//————————————————标尺
        }

        /***************************************显示双缓冲的图片bt************************************/
        /**************************************显示工作步骤，在此方法中调用***************************/
        //public int sr_Flag = 0;//是否选中图片标志位————————————修改为public
        //public Point firstpoint;//修改为public
        //public Point secondpoint;//修改为public
        public void Select_rectangle(Graphics gra, int xsize, int ysize)//————————————+——————————————————————————修改了形参和级别
        {
            if (sr_Flag == 0)//画LINK的时候，要不要继续添加画方框的标志位
            {
                Pen drawPen = new Pen(Color.DarkSlateBlue, (float)0.5);
                drawPen.DashPattern = new float[] { 3, 3 };

                LINK(gra,xsize,ysize);//是把数据放到上面——————重绘所有的图片到bt——————画图片，画背景，画坐标值

                SolidBrush b1 = new SolidBrush(Color.FromArgb(50, Color.Green));//画方框
                Rectangle r = new Rectangle(firstpoint.X, firstpoint.Y, (secondpoint.X - firstpoint.X), secondpoint.Y - firstpoint.Y);//画方框
                //Write("鼠标绘制矩形方框");//画方框————要去掉，不然会报错
                gra.DrawRectangle(drawPen, r);//画方框
                gra.FillRectangle(b1, r);//画方框           
            }
            else//画LINK的时候，不要继续添加画方框的标志位，只画LINK就行
            {
                Pen drawPen = new Pen(Color.DarkSlateBlue, (float)0.5);
                drawPen.DashPattern = new float[] { 3, 3 };

                LINK(gra, xsize, ysize);//是把数据放到上面——————重绘所有的图片到bt——————画图片，画背景，画坐标值
                sr_Flag = 0;//——————————————————————————————————————————————————————不画之后，下次就要画方框
            }
        }
        /******************************************输出日志****************************************/
        public void Write(string msg)
        {
            //获取当前程序目录
            string logPath = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
            //新建文件
            System.IO.StreamWriter sw = System.IO.File.AppendText(logPath + "/日志.txt");
            //写入日志信息
            sw.WriteLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss ") + msg);
            //关闭文件
            sw.Close();
            sw.Dispose();
        }
    }
}
