using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReadFile;//Composation和BinderJetting命名空间均包含了此读取命名控件

namespace Composation//统一修改为BinderJetting命名空间
{
    class ComposationCLI
    {
        public List<JobItem> jobItems = new List<JobItem>();//save the INFO from JobItems
        public int jobItemsCount = 0;//Record the NUM of TIFF data：记录总的零件的个数
        //CountNum is the number of TIFF Path， CountNum is contained in the CliStreams
        public void Composation(ref List<CLI> CliStreams,double innerSafeGap,double borderSafeGap/*, string CadOperationCode*/)// 内部安全和外部安全距离默认采取2mm
        {
            //int CountNum = CliStreams.Count;
            //Initialization: inputing data and sorting the TIFF PATH according to the TIFF area 
            InitiateTIFF(ref CliStreams/*, CadOperationCode*/);//recordPath存了Paths, CliStreams

#if false//20200513注释：本部分排版任务，移到Magics中实现
            SortTIFF(jobItems,ref CliStreams);//在生成新的jobItems序列的同时，CliStreams序列也必须得到维护
            //Initiate the tempX:
            //double tempX = 420-2* borderSafeGap;//record the spare row length in BASE, and the iniatiate value is 420-2*borderSafeGap
            //double tempY = 350-2* borderSafeGap;//record the spare colunm length in BASE , and the iniatiate value is 350-2*borderSafeGap
            //Initiate the position:
            jobItems[0].position.X = borderSafeGap;//the initianate value is bordersafeGap
            jobItems[0].position.Y = borderSafeGap;//the initianate value is bordersafeGap

            int tempColumnNum = 1;//as a temporary conter to count the item number in a previous row, the first NUM is 1, not 0

            for (int k=1;k<jobItems.Count;k++)//Go through all the TIFF path, the first ID is 1, not the 2
            {
                if (jobItems[k-1].position.X+ jobItems[k-1].Width + jobItems[k].Width+innerSafeGap<420-borderSafeGap)//if a row is not arrange well yet.//Arrange a row:
                {                 
                    //Calculate the new position:
                    jobItems[k].position.X = jobItems[k-1].position.X+ jobItems[k-1].Width+innerSafeGap;
                    jobItems[k].position.Y = jobItems[k - 1].position.Y;
                    //jobItems[k].position.Y = 350- borderSafeGap- tempY;
                    //Calculate the new tempX:
                    //tempX = tempX- jobItems[k].Width-innerSafeGap;//TEMPX - 占用的1个零件的宽度
                    tempColumnNum++;
                }
                else//when a row is arrange well.//Switch Rows: 
                {
                    //Arrange a new row:
                    double PreviousMaxRowHeight =CalculateRowsHeight(k,tempColumnNum);//Calculate the previous row's max Height,计算上一行的最大高度                    
                    //SwitchRow(PreviousMaxRowHeight);//Switch Rows: it is not the main part，代码量比较少
                    jobItems[k].position.X = borderSafeGap;
                    jobItems[k].position.Y = jobItems[k-1].position.Y+PreviousMaxRowHeight+innerSafeGap;

                    //tempX= 420 - 2 * borderSafeGap- jobItems[k].Width - innerSafeGap;
                    //tempY = 350 - borderSafeGap - tempY - PreviousMaxRowHeight;
                    tempColumnNum = 1;//reset the tempColumnNum when a new row is already start
                }
            }
#endif
        }

        //when ref 使用之前必须初始化    
        public void InitiateTIFF(ref List<CLI> CliStreams/*, string CadOperationCode*/)//玩一玩引用吧，要不怎么办？内存空间太大了
        {
            jobItems.Clear();
            //JobItem tempJobItem = new JobItem();//差点搞死
            for (int i=0;i<= CliStreams.Count-1;i++)//必须进行深复制修改——结果：不必须深复制修改
            {
                //List<CLI> CLIs = CliStreams.ConvertAll(cli=>new CLI());
                //List<CLI> CLIs = CliStreams.Select(cli => new CLI()).ToList();
                JobItem tempJobItem = new JobItem();//差点搞死

                tempJobItem.id = CliStreams.ElementAt(i).ID;//initiate the picture ID
                tempJobItem.Path = CliStreams.ElementAt(i).recordPathItem;//initiate the picture ID

                tempJobItem.Width = CliStreams.ElementAt(i).Dimension[1].x- CliStreams.ElementAt(i).Dimension[0].x;//read the TIFF data's width
                tempJobItem.Height = CliStreams.ElementAt(i).Dimension[1].y - CliStreams.ElementAt(i).Dimension[0].y;//read the TIFF data's height
                tempJobItem.area = tempJobItem.Width * tempJobItem.Height;
#if false//true:在本软件中重排
                //Following part should be optimization in the next Stage
                tempJobItem.position.X = 0;//暂时先不设置，直接reset为0；之后支持在magics中进行完成的排版文件的导入————！！！！！
                tempJobItem.position.Y = 0;//暂时先不设置，直接reset为0；之后支持在magics中进行完成的排版文件的导入————！！！！！
#else//20200513修改：false:在Magics中重排
                tempJobItem.position.X = CliStreams.ElementAt(i).Dimension[0/*1*/].x;//暂时先不设置，直接reset为0；之后支持在magics中进行完成的排版文件的导入————！！！！！//20200514xiugai
                tempJobItem.position.Y = CliStreams.ElementAt(i).Dimension[0/*1*/].y;//暂时先不设置，直接reset为0；之后支持在magics中进行完成的排版文件的导入————！！！！！//20200514xiugai
                
                //if (CadOperationCode=="1")
                //{
                //    double originalY/*tempJobItem.position.Y */= CliStreams.ElementAt(i).Dimension[0/*1*/].y;//暂时先不设置，直接reset为0；之后支持在magics中进行完成的排版文件的导入————！！！！！//20200514xiugai
                //   tempJobItem.position.Y = -originalY + 330 / 2;//20230320修改：修复坐标系不协调的问题
                //}

                tempJobItem.deltaX = CliStreams.ElementAt(i).Dimension[2/*1*/].x;//X偏移值：20201113新增：
                tempJobItem.deltaY = CliStreams.ElementAt(i).Dimension[2/*1*/].y;//Y偏移值：20201113新增：
#endif
                jobItems.Add(tempJobItem);
            }
        }
        //using Bubble Sorting to sort the JobItem according to the area firstly.
        //sorting the CliStreams.
        public void SortTIFF(List<JobItem> jobItems,ref List<CLI> CliStreams) //TIFFpath的信息重新排序——冒泡法根据面积进行初次排序
        {
            JobItem tempItems = null;//临时存放面积数据
            //List<CLI> tempCliStreamItems = null;//临时存放CLIStrems数据

            //不需要调整CLIStreams的位置，会造成1连串的影响。尽量保持数据的单向修改和传递。
            //至于遇到的问题，通过CLI的ID检索即可
            //CliStreams.Add(null);//新增作为临时变量的最后一项
            for (int i = 0; i<jobItems.Count-1; i++)
            {
                for (int j = 0; j <jobItems.Count-1-i; j++)
                {
                    if (jobItems[j].area < jobItems[j + 1].area)//从小到大排列
                    {
                        tempItems = jobItems[j + 1];
                        //CliStreams[CliStreams.Count-1] = CliStreams[j+1];//CliStreams[CliStreams.Count-1]就是tempCliStreamItems

                        jobItems[j + 1] = jobItems[j];
                        //CliStreams[j + 1] = CliStreams[j];

                        jobItems[j] = tempItems;
                        //CliStreams[j] = CliStreams[CliStreams.Count - 1];
                    }
                }
            }
            //CliStreams.RemoveAt(CliStreams.Count - 1);//删掉作为临时变量的最后一项
        }
        ////排列新的1行——执行具体的排列行的操作
        //public void ArrangeRows(int k,double tempX, double innerSafeGap, double borderSafeGap){}//arrange a new row
        //public void SwitchRow(double PreviousMaxRowHeight) { }//Switch Rows: 排列新的1列——执行新增列的操作; PreviousMaxRowHeight is the TIFF data id that will show firstly in new row
        public double CalculateRowsHeight(int k,int tempColumnNum )//计算上一行的最大高度
        {
            double tempMaxHeight=0;
            for (int j = 0; j < tempColumnNum; j++)
            {              
                if (jobItems[k-tempColumnNum+j].Height > tempMaxHeight)//从小到大排列
                {
                    tempMaxHeight = jobItems[k - tempColumnNum + j].Height;
                }
                else
                {}
            }
            return tempMaxHeight;
        }
    }
    class TempPoint
    {
        public double X=0, Y=0;//X,Y分别为点类的坐标
    }
    class BinderingParameter { }//喷射参数
    class LightingParameter { }//固化参数
    class LayerParameter { }//层厚参数
    class Parameter//参数区汇总
    {
        public BinderingParameter binderingParameter=new BinderingParameter();
        public LightingParameter lightingParameter = new LightingParameter();
        public LayerParameter layerParameter = new LayerParameter();
    }
    class Edition { }
    class JobItem//this DATA structure is very important for the JOB Management & Configuration
    {
        public int id =0;//TIFF ID INFO
        public string Path=null;//TIFF PATH INFO
        public double Width=0;//TIFF width
        public double Height=0;//TIFF height
        public double area=0;//TIFF area
        public TempPoint position = new TempPoint();//TIFF position
        public Parameter parameter = new Parameter();//the JOB working parameter
        public Edition edition = new Edition();//the TIFF DATA edtion: contains time and edition infomation
        public double deltaX = 0;//X偏移值：20201113新增：
        public double deltaY = 0;//Y偏移值：20201113新增：
    }

    //20200223新增:多线程绘制图像数据
    //20200223新增:多线程绘制图像数据
    class Integral_interface_data//20200223：原来是我定义的？
    {
        //20200225新增:多线程绘制图像数据
        //20200225新增:多线程绘制图像数据
		public Point s_min, s_max;//——————选中图片区域左上角和右下角的点的坐标
        public int num_p = 0;//变量，显示计数变量，第几张图片
        public List<int> PX = new List<int>();//像素坐标X
        public List<int> PY = new List<int>();//像素坐标Y
        public List<int> px = new List<int>();//坐标轴坐标x
        public List<int> py = new List<int>();//坐标轴坐标y
        public List<int> PH = new List<int>();//图片的高度
        public List<int> PW = new List<int>();//图片的宽度
        public List<int> LAYER = new List<int>();//图片所在层数
        public List<int> FLAG = new List<int>();//图片是否选中的标志位
        //20200225新增:多线程绘制图像数据
        //20200225新增:多线程绘制图像数据
		
        //鼠标按下坐标、缩放数
        public Point MouseDownP = new Point();//鼠标按下时坐标      
        public int zoom = 1; //初始放大倍数//————修改成double，看是否可以public int zoom = 0;//差点被你搞死
                             //            public int pixel2MM = 2;//初始缩放比例

        public bool ResetFlag = true;//刷新输入STL零件的标志位

        public int CLInum = 0;//常量：the num of the TiffNum in PATH，it can be modified in the MAINFORM, and it must be public.

        public List<JobItem> tempJobItems = new List<JobItem>();//全部的job参数，包含了所有STL位置、参数

        static string RESOURSE;//图片的来源路径,此处暂时先不改！！！

        public List<string> TIFFPathCopy = new List<string>();//record the TIFF FILE

        public int sr_Flag = 0;//是否选中图片标志位————————————修改为public
        public Point firstpoint;//修改为public
        public Point Firstpoint;
        public Point secondpoint;//修改为public
        public Point Secondpoint;

        //           public Bitmap bt = new Bitmap(3000, 3000);//画在这张图片上面————————————————————————进行了修改！！！！！
        //           Image img;

        public float img_x = 620, img_y = 20 + 700 / 2, MouseDown_x, MouseDown_y,
            MouseUp_x = 0, MouseUp_y = 0, MouseWheel_x = 0, MouseWheel_y = 0;//————————————————修改为public
        public int img_w, img_h, select_Flag = 0;
        public int mov_F = 0;
        public double Time;
    }
    //20200223新增:多线程绘制图像数据
    //20200223新增:多线程绘制图像数据
}
