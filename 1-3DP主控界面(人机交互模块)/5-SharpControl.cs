using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using SharpDX.D3DCompiler;
using SharpDX.Windows;
using SharpDX.DirectWrite;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Composation;//20200527框架移植：
using SharpDX.WIC;//20200609新增：

namespace BinderJetting
{
    using Buffer = SharpDX.Direct3D11.Buffer;
    using Device = SharpDX.Direct3D11.Device;
    using DXGIDevice = SharpDX.DXGI.Device;
    using D2D1Device = SharpDX.Direct2D1.Device;
    using DeviceContext = SharpDX.Direct2D1.DeviceContext;
    using DXGIFactory = SharpDX.DXGI.Factory;
    using SwapChain = SharpDX.DXGI.SwapChain;
    using Surface = SharpDX.DXGI.Surface;

    //20200609新增：
    using AlphaMode = SharpDX.Direct2D1.AlphaMode;
    using Bitmap = SharpDX.WIC.Bitmap;
    using PixelFormat = SharpDX.Direct2D1.PixelFormat;
    using System.Runtime.InteropServices;
    using System.Drawing.Imaging;
    using System.IO;
    using SharpDX.IO;
    using System.Collections;

    class SharpControl
    {
        public RYSYSParam gc_RysysParam = new RYSYSParam();//20210113新增：用于修改大零件打印子区域处理算法的相关参数：

        public List<JobItem> tempJobItems = new List<JobItem>();//全部的job参数，包含了所有STL位置、参数//20200527框架移植：

        private List<RemoteCLIs> gc_RemoteCLIs = new List<RemoteCLIs>();//20201117批注：GUI端数据
        private List<RemoteCLIs> gc_RemoteCLIs2 = new List<RemoteCLIs>();//20201117批注：后台打印端数据
        private int LayerCount = 0;
        public List<RemoteCLIs> ToRemoteCLIsList//添加属性
        {
            set { gc_RemoteCLIs = value; }
            //get { return gc_RemoteCLIs; }
        }
        public List<RemoteCLIs> ToRemoteCLIsList2//添加属性
        {
            set { gc_RemoteCLIs2 = value; }
            //get { return gc_RemoteCLIs2; }
        }
        public int UILayerCount//添加属性
        {
            set { LayerCount = value; }
            get { return LayerCount; }
        }

        public bool ModifyColorSysFlag = false;//20200612新增:在主界面主菜单应用触发
        public RawColor4[] InputColorCards = new RawColor4[8];//20200612修改：UI风格的统一设置

        DeviceContext deviceContext;
        SwapChain SwapChain2;
        Surface surface;
        Device d3DDevice;
        DXGIDevice dxgiDevice;
        D2D1Device d2DDevice;
        Bitmap1 targetBitmap;
        SwapChainDescription swapChainDesc;
        //20200523新增：
        SharpDX.DirectWrite.Factory dwFactory;
        TextFormat textFormat;
        TextFormat textFormat2;
        TextFormat textFormat3;
        TextFormat textFormat4;
        TextLayout textLayout;
        TextLayout textLayout2;
        TextLayout textLayout3;
        Texture2D backBuffer2;//20200525新增
        /// <summary>
        /// (1)渲染初始化：(1)渲染控件句柄;(2)渲染控件尺寸//renderControl1.Handle;renderControl1.Width * 2;renderControl1.Height * 2
        /// </summary>
        public void InitiateDevice(IntPtr ControlHandle, int Width, int Height) //20200521:
        {
            //SharpDX.Configuration.EnableObjectTracking = true;//20210308新建：//Reference with:https://stackoverflow.com/questions/25836881/memory-leak-in-my-sharpdx-application

            // 创建 Dierect3D 设备。
            d3DDevice = new Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport/*Debug*//*BgraSupport*/);
            dxgiDevice = d3DDevice.QueryInterface<Device>().QueryInterface<DXGIDevice>();
            // 创建 Direct2D 设备和工厂。
            d2DDevice = new D2D1Device(dxgiDevice);
            this.deviceContext = new DeviceContext(d2DDevice, DeviceContextOptions.EnableMultithreadedOptimizations/*None*/);
            //deviceContext.PixelSize = new Size2(renderControl1.Width/**4*/, renderControl1.Height/**4*/);//像素尺寸
            Size2F size2F = new Size2F/*(96*4, 96*4)*/(96 * 2f, 96 * 2f);//设备无关像素单位设置：≈=物理单位设置
            deviceContext.DotsPerInch = size2F;
            deviceContext.UnitMode = 0;
            // 创建 DXGI SwapChain。
            swapChainDesc = new SwapChainDescription()
            {
                BufferCount = 1,
                Usage = Usage.RenderTargetOutput,
                OutputHandle = ControlHandle,
                IsWindowed = true,
                // 这里宽度和高度都是 0，表示自动获取。ModeDescription = new ModeDescription(0, 0, new Rational(60, 1), Format),
                //后台缓冲区描述
                ModeDescription = new ModeDescription(
                Width * 2,                       //windows veiwable width：渲染窗口的宽度
                Height * 2,                      //windows veiwable height：渲染窗口的高度
                new Rational(60, 1),                          //refresh rate：刷新速度：60H
                Format.R8G8B8A8_UNorm),
                SampleDescription = new SampleDescription(1, 0),
                SwapEffect = SwapEffect.Discard
            };

            this.SwapChain2 = new SwapChain(dxgiDevice.GetParent<Adapter>().GetParent<DXGIFactory>(),
                d3DDevice, swapChainDesc);

            //var d2dFactory = new SharpDX.Direct2D1.Factory();
            backBuffer2 = Texture2D.FromSwapChain<Texture2D>(SwapChain2, 0);
            this.surface/*backBuffer*/ = backBuffer2.QueryInterface<Surface>();
            targetBitmap = new Bitmap1(this.deviceContext, surface);
            //var d2dRenderTarget = new RenderTarget(d2dFactory, surface,
            //new RenderTargetProperties(new PixelFormat(Format.Unknown, SharpDX.Direct2D1.AlphaMode.Premultiplied)));
            this.deviceContext.Target = targetBitmap;

            // Create a solid color brush.
            solidBrush = new SolidColorBrush(deviceContext, SharpDX.Color.Coral);
            OutlineBrush = new SolidColorBrush(deviceContext, SharpDX.Color.Black);
            solidBrush3 = new SolidColorBrush(deviceContext, SharpDX.Color./*White*/Black);
            solidBrushBackLable = new SolidColorBrush(deviceContext, SharpDX.Color.LightBlue/*.Black*/);
            solidBrushTopLeftLable = new SolidColorBrush(deviceContext, SharpDX.Color.Yellow/*.Black*/);

            LocationHoleBrush = new SolidColorBrush(deviceContext, SharpDX.Color.CornflowerBlue/*new RawColor4(76, 100, 238, 255)*//*SharpDX.Color.LightSkyBlue*//*CadetBlue*//*BlueViolet*/);
            PartSolidBrush = new SolidColorBrush(deviceContext, SharpDX.Color.Blue /*new SharpDX.Color(0, 0, 0, 150)*/ /*SharpDX.Color.LightGreen*//*SharpDX.Color.LightSkyBlue*//*LightBlue*//*CadetBlue*//*BlueViolet*/);
            PartSolidBrush2 = new SolidColorBrush(deviceContext, SharpDX.Color.White /*new SharpDX.Color(0, 0, 0, 150)*/ /*SharpDX.Color.LightGreen*//*SharpDX.Color.LightSkyBlue*//*LightBlue*//*CadetBlue*//*BlueViolet*/);

            BaseBrush = new SolidColorBrush(deviceContext, SharpDX.Color.White/*LightGoldenrodYellow*//*LightYellow*/);
            RulerBackBrush = new SolidColorBrush(deviceContext, SharpDX.Color.CornflowerBlue);
            RulerBackBrush2 = new SolidColorBrush(deviceContext, SharpDX.Color.CornflowerBlue);
            RulerLineBrush = new SolidColorBrush(deviceContext, SharpDX.Color.Black);//20200612新增
            BaseLineBrush = new SolidColorBrush(deviceContext, SharpDX.Color.Black);//20200612新增
            CoordinatLineBrush = new SolidColorBrush(deviceContext, SharpDX.Color.BlueViolet/*Blue*//*Black*/);//20200612新增
            CornerCoverBrush = new SolidColorBrush(deviceContext, SharpDX.Color.LightBlue);//20200612新增


            Factory2D = new SharpDX.Direct2D1.Factory();
            styleProperties = new StrokeStyleProperties()
            {
                //StartCap = CapStyle.Square,
                //EndCap = CapStyle.Square,
                MiterLimit = 5f,
                DashOffset = 5f,

                DashStyle = DashStyle.DashDot
            };

            styleProperties2 = new StrokeStyleProperties()
            {
                //StartCap = CapStyle.Square,
                //EndCap = CapStyle.Square,
                MiterLimit = 5f,
                DashOffset = 5f,
                DashStyle = DashStyle.Solid
            };


            //styleProperties.DashStyle = DashStyle.Dash;
            strokeStyle2 = new StrokeStyle(/*deviceContext.Factory*/d2DDevice.Factory, styleProperties);
            strokeStyle3 = new StrokeStyle(/*deviceContext.Factory*/d2DDevice.Factory, styleProperties);
            strokeStyle4 = new StrokeStyle(/*deviceContext.Factory*/d2DDevice.Factory, styleProperties2);//20201111新建：背景线改为实线

            styleProperties1 = new StrokeStyleProperties()
            {
                StartCap = CapStyle.Square,
                EndCap = CapStyle.Square,
            };
            //styleProperties.DashStyle = DashStyle.Dash;
            strokeStyle1 = new StrokeStyle(/*deviceContext.Factory*/d2DDevice.Factory, styleProperties1);

            // Create a linear gradient brush.
            // Note that the StartPoint and EndPoint values are set as absolute coordinates of the surface you are drawing to,
            // NOT the geometry we will apply the brush.
            linearGradientBrush = new LinearGradientBrush(deviceContext, new LinearGradientBrushProperties()
                {
                    StartPoint = new Vector2(50, 0),
                    EndPoint = new Vector2(450, 0),
                },
                new GradientStopCollection(deviceContext, new GradientStop[]
                {
                    new GradientStop()
                    {
                        Color = SharpDX.Color.Red/*CornflowerBlue*//*SharpDX.Mathematics.Interop.RawColor4(Color.Green.R, Color.Green.G, Color.Green.B, Color.Green.A)*/,
                        Position = 0,
                    },
                    new GradientStop()
                    {
                        Color = SharpDX.Color.Yellow /*new SharpDX.Mathematics.Interop.RawColor4(Color.Green.R, Color.Green.G, Color.Green.B, Color.Green.A)*/,
                        Position = 1,
                    }
                }));

            gradientStop2 = new GradientStop() { Color = InputColorCards[0]/*SharpDX.Color.White*/, Position = 0, };
            //gradientStop2.Color = InputColorCards[1]; gradientStop2.Position = 1;
            linearGradientBrushProperties = new LinearGradientBrushProperties() {StartPoint = new Vector2(0, 0), EndPoint = new Vector2(0, 600)};
            //linearGradientBrushProperties.StartPoint = new Vector2(0, 0); linearGradientBrushProperties.EndPoint = new Vector2(0, ControlRectangle.Height);
            gradient2Stops=new GradientStop[2];//20210309新增
            gradient2Stops[0] = gradientStop1; gradient2Stops[1] = gradientStop2;
            //(3)等待堆区释放，重新分配堆区，刷新显示
            gradientStops = new GradientStopCollection(deviceContext, gradient2Stops/*new GradientStop[] {gradientStop1,gradientStop2}*/);
            vector2 = new Vector2(0, 0);//20210309新增
            vector3 = new Vector2(0, 0);//20210309新增
            rawVector2 = new RawVector2(0/*xsize-248*/, 0/*ysize - 58*/);


            //20200523新建：DirectWrite文字显示
            dwFactory = new SharpDX.DirectWrite.Factory();
            textFormat = new TextFormat(dwFactory, "Arial", FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Oblique/*Italic*//*Normal*/, 20);
            textFormat2 = new TextFormat(dwFactory, "Arial", FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, 48);
            textFormat3 = new TextFormat(dwFactory, "Arial", FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Oblique/*Italic*//*Normal*/, 20);
            textFormat4 = new TextFormat(dwFactory, "Arial"/*"宋体"*/, FontWeight.SemiBold/*Medium*//*Light*//*Regular*//*UltraBold*/, SharpDX.DirectWrite.FontStyle.Italic/*Normal*/, /*SharpDX.DirectWrite.FontStretch.UltraExpanded,*/ /*30*/28f);//20210308新增：非常关键，对于解决内存溢出问题非常关键

            textLayout = new TextLayout(dwFactory, "LASERADD-SOFTWARE", textFormat, float.PositiveInfinity, float.PositiveInfinity);
            //textFormat2.ReadingDirection = 3;
            textLayout2 = new TextLayout(dwFactory, "123sss", textFormat2, float.PositiveInfinity, float.PositiveInfinity);
            //textLayout3 = null;//20210309修改：修复内存泄漏问题
            textLayout3 = new TextLayout(dwFactory, "0"/*CurrentLayer*/, textFormat3, float.PositiveInfinity, float.PositiveInfinity);//201810100309：存在内存泄漏//初始赋值0可能存在某些未知问题，但影响 不大


            geometry = new PathGeometry(deviceContext.Factory);//20210309新建批注：此处需要完成对应的修改//本行代码非常关键
            geometry3 = new PathGeometry(deviceContext.Factory);//20210310修改：//本行代码非常关键

            //StartRenderLoopTimer();
#if false
            //20200524新增：
            MouseWheel += new MouseEventHandler(Form1_MouseWheel);//添加鼠标滚轮事件//迁移到InitializeComponent中间
#endif
        }
        private SharpDX.Direct3D11.Buffer d3dbuffer;//20200509新增
        private SharpDX.Direct3D11.Device/*1*/ device;
        private SharpDX.Direct3D11.DeviceContext1 d3dContext;
        private SharpDX.Direct2D1.DeviceContext d2dContext;
        private SwapChain1 SwapChain;//this is only run with WIN RT————SwapChain1继承自SwapChain//20200510新建
        private SharpDX.Direct2D1.Bitmap1 d2dTarget;

        private SolidColorBrush solidBrush;
        private SolidColorBrush LocationHoleBrush;
        private SolidColorBrush PartSolidBrush, PartSolidBrush2;
        private SolidColorBrush BaseBrush;
        private SolidColorBrush RulerBackBrush, RulerBackBrush2;//20200612新增
        private SolidColorBrush RulerLineBrush;//20200612新增
        private SolidColorBrush BaseLineBrush;//20200612新增
        private SolidColorBrush CoordinatLineBrush;//20200612新增
        private SolidColorBrush CornerCoverBrush;//20200612新增
        private SolidColorBrush OutlineBrush;

        private LinearGradientBrush linearGradientBrush;//20210309新增：避免内存泄漏
        private RadialGradientBrush radialGradientBrush;
        private StrokeStyleProperties styleProperties, styleProperties1, styleProperties2;
        private StrokeStyle strokeStyle3,strokeStyle2, strokeStyle1, strokeStyle4;

        private GradientStop gradientStop1;//20210309新增：
        private GradientStop gradientStop2;//20210309新增：
        GradientStop[] gradient2Stops;
        private LinearGradientBrushProperties linearGradientBrushProperties;//20210309新增：
        private GradientStopCollection gradientStops;//20210309新增：
        Vector2 vector2 = new Vector2(0, 0);//20210309新增
        Vector2 vector3 = new Vector2(0, 0);//20210309新增
        RawVector2 rawVector2 = new RawVector2(0,0);//20210309新增


        //private SharpDX.Direct2D1.GeometrySink GeometrySink;//复杂形状接口：指定一系列由直线、曲线、弧线组成的图形
        private SharpDX.Direct2D1.GeometrySink geometrySink;//复杂形状接口：指定一系列由直线、曲线、弧线组成的图形

        /******************************************************************************/
        /******************************************************************************/
        public SharpDX.Direct2D1.Factory Factory2D { get; private set; }
        public SharpDX.DirectWrite.Factory FactoryDWrite { get; private set; }
        public WindowRenderTarget RenderTarget2D { get; private set; }
        public SolidColorBrush SceneColorBrush { get; private set; }
        public SolidColorBrush solidBrushBackLable { get; private set; }
        public SolidColorBrush solidBrushTopLeftLable { get; private set; }
        public SharpDX.DirectWrite.Factory factory;
        private PointF CursorPointF = new PointF(0, 0);//20200524新增：
       
        SharpDX.RectangleF rect1;
        SolidColorBrush solidBrush3;
        long tag1, tag2;
        /// <summary>
        /// (2)执行渲染：20200526新建
        /// </summary>
        /// <param name="ControlRectangle"></param>
        public void DrawDevice(bool CLIImportFlag, int CLIlayerIndex, SharpDX.RectangleF ControlRectangle)//20200521:
        {
            if (ModifyColorSysFlag==true )//统一更新画刷系统色彩
            {
                //InputColorCards[0] = SharpDX.Color.White;//2D背景色1：
                //InputColorCards[1] = SharpDX.Color.LightGray;//2D背景色2：
                //InputColorCards[2] = SharpDX.Color.White;//基板背景色：
                //InputColorCards[3] = SharpDX.Color.CornflowerBlue;//标尺背景色：
                //InputColorCards[4] = SharpDX.Color.Black;//1级网格色：
                //InputColorCards[5] = SharpDX.Color.Black;//2级网格色：
                //InputColorCards[6] = SharpDX.Color.OrangeRed;//定位孔色：
                //InputColorCards[7] = SharpDX.Color.Black;//标尺前景色：修改为实体零件背景色

                BaseBrush.Color = InputColorCards[2];
                RulerBackBrush.Color = InputColorCards[3];
                RulerLineBrush.Color = InputColorCards[4];
                BaseLineBrush.Color= InputColorCards[5];
                LocationHoleBrush.Color = InputColorCards[6];
                PartSolidBrush.Color = InputColorCards[7];//实体零件背景色
                PartSolidBrush.Opacity = 1f;

                ModifyColorSysFlag = false;

#if false//20200612批注：测试成功后，再修改
                OutlineBrush.Color = SharpDX.Color.Black;//描边色：
                OutlineBrush.Color = InputColorCards[8];
                CoordinatLineBrush.Color = InputColorCards[5];//中心线色
                CornerCoverBrush.Color= InputColorCards[5];//左上角覆盖件背景色
#endif
            }

#if true
            deviceContext.BeginDraw();
            deviceContext.Clear(SharpDX.Color.LightGray/*new RawColor4(182,182,182,200)*//*SharpDX.Color.Aquamarine*//*WhiteSmoke*//*CornflowerBlue*/);
            deviceContext.UnitMode = UnitMode.Dips;
            Size2F size2F = new Size2F(96 * 2f, 96 * 2f);//设备无关像素单位设置：≈=物理单位设置
            deviceContext.DotsPerInch = size2F;
            deviceContext.AntialiasMode = AntialiasMode./*PerPrimitive*/Aliased;//抗锯齿模式//20210317修改：抗锯齿模式

            //20200605批注：UI渐变色//更新linearGradientBrush,绑定并分配堆区//20210309修改：
            //(1)释放堆区内存：
            linearGradientBrush.Dispose();//释放内存
            gradientStops.Dispose();//释放内存
            //(2)修改显示属性值
            //gradientStop1 = new GradientStop() { Color = InputColorCards[0]/*SharpDX.Color.White*/, Position = 0, };
            gradientStop1.Color = InputColorCards[0]; gradientStop1.Position = 0;
            //gradientStop2 = new GradientStop() { Color = InputColorCards[0]/*SharpDX.Color.White*/, Position = 0, };
            gradientStop2.Color = InputColorCards[1]; gradientStop2.Position = 1;
            //Vector2 vector2=  new Vector2(0, 0);
            //Vector2 vector3 = new Vector2(0, 0);
            vector2.X = 0; vector2.Y = 0;
            vector2.X = 0; vector3.Y = ControlRectangle.Height;
            //linearGradientBrushProperties = new LinearGradientBrushProperties() {StartPoint = new Vector2(0, 0), EndPoint = new Vector2(0, ControlRectangle.Height),};
            linearGradientBrushProperties.StartPoint = /*new Vector2(0, 0)*/vector2; linearGradientBrushProperties.EndPoint = /*new Vector2(0, ControlRectangle.Height)*/vector3;
            //GradientStop[] gradient2Stops=new GradientStop[2];
            gradient2Stops[0] = gradientStop1; gradient2Stops[1] = gradientStop2;


            //(3)等待堆区释放，重新分配堆区，刷新显示
            while ((linearGradientBrush.IsDisposed == false)|| (gradientStops.IsDisposed == false))
            {
                Thread.Sleep(1);//等待20ms
            }
            gradientStops = new GradientStopCollection(deviceContext, gradient2Stops);
            linearGradientBrush = new LinearGradientBrush(deviceContext/*重新分配内存*/, linearGradientBrushProperties/*梯度轴*/, gradientStops);


            RawRectangleF rectangleF = new RawRectangleF(0, 0, ControlRectangle.Width/*192*/, ControlRectangle.Height/*192*/);//左上右下//Draw Base contoul and back: 绘制基板轮廓背景
            deviceContext.FillRectangle(rectangleF, linearGradientBrush);
            DrawImgCoord2(CLIImportFlag, CLIlayerIndex, ControlRectangle.Width, ControlRectangle.Height);
            Result EndDrawResult = deviceContext.TryEndDraw(out tag1, out tag2);//deviceContext.EndDraw(); //long a, b;deviceContext.TryEndDraw(out a,out b);
            while (EndDrawResult != Result.Ok)
            {
                Thread.Sleep(1);//等待20ms//20210309修改为5ms
                EndDrawResult = deviceContext.TryEndDraw(out tag1, out tag2);
            }

            SwapChain2.Present(1, PresentFlags./*DoNotWait*/None);
#endif
        }

        PathGeometry geometry;
        PathGeometry geometry3;//20210310新增：

        private PointF viewportbase = new PointF(0, 0);//20200522新增：
        private float borderx1, bordery1, borderx2, bordery2;//201106新增：选中操作区域实际上是2点矩形;
        public List<string> selectPaths = new List<string>();//全局存放读取的CLI的文件名
        public/*private*/ int g_CorrectionFigureFlag = 0/*0*//*1*//*0*/;//20210306新增：0为正常打印模式；1-2-3-4依次为4张校准图

        CLISubAeraAlgorithm cLISubAeraAlgorithm = new CLISubAeraAlgorithm();//20210112新增：CLI子区域算法接口
        /// <summary>
        /// (3)渲染指令设置：20200526新建
        /// </summary>
        /// <param name="xsize"></param>
        /// <param name="ysize"></param>
        private void DrawImgCoord2(bool CLIImportFlag, int CLIlayerIndex, float xsize, float ysize)//绘制坐标系的标尺
        {
#if true
            //(0)计算缩放前的坐标点处的实际坐标值————计算缩放后的的坐标点出的实际坐标值————计算两者之间的差值————换算成之间的坐标原点的偏移值。
            if (initFLag == false)//如果像素坐标原点没有初始化，则进行初始化
            {
                viewportbase.X = xsize / 2 + 3;
                viewportbase.Y = ysize / 2;
                initFLag = true;

                m_zoomScaleBetween = 0.5f * 120f/*e.Delta*/ / 120f/*zDelta/120f*/;//20200524批注：缩放间距
                m_zoomScale = 0.5f/*1.2f*/;
                mainRuler = 50;
            }
            else if (initFLag == true && RePaintFlag == false)
            {
                viewportbase.X = (CursorPointF.X + ((viewportbase.X - CursorPointF.X) / (m_zoomScale - m_zoomScaleBetween) * m_zoomScale));
                viewportbase.Y = (CursorPointF.Y + ((viewportbase.Y - CursorPointF.Y) / (m_zoomScale - m_zoomScaleBetween) * m_zoomScale));
                //(viewportbase.X-CursorPointF.X)*m_zoomScaleBetween)为缩放一次后鼠标点相对于像素原点的像素差值。
                //((viewportbase.X-CursorPointF.X)*m_zoomScaleBetween)/m_zoomScale;为缩放一次后鼠标点的实际坐标的发生的偏移量。
            }
            else if (initFLag == true && RePaintFlag == true)//20200524新建：不进行任何处理，避免重绘导致的BUG
            {
                RePaintFlag = false;//标志位复位
            }
            else { }
#endif
            //(1)Draw Base contoul and back: 绘制基板轮廓背景          
            Matrix3x2 myMatrix = new Matrix3x2(1, 0, 0, -1, 0, 0);//Y向坐标系翻转://世界坐标系坐标系变换：单位像素，全局变换
            Vector2 vector1 = new Vector2(0.5f * m_zoomScale, 0.5f * m_zoomScale);//非常关键：20200524新增
            myMatrix.ScaleVector = vector1;//本机电脑上显示比例与实际比例差别
            Vector2 vector2 = new Vector2(/*xsize / 2 + 3, ysize / 2*/viewportbase.X, viewportbase.Y);//非常关键：20200524新增
            myMatrix.TranslationVector = vector2;//坐标系平移
            deviceContext.Transform = myMatrix;

            RawRectangleF rectangleF = new RawRectangleF(-165f/*210f*/ * mm2Dip, -165f/*175f*/ * mm2Dip, 165f/*210f*/ * mm2Dip, 165f/*175f*/ * mm2Dip);//左上右下//Draw Base contoul and back: 绘制基板轮廓背景
            deviceContext.FillRectangle(rectangleF, BaseBrush);
            deviceContext.DrawRectangle(rectangleF, OutlineBrush, 1.25f / (0.5f * m_zoomScale));


            //(4)绘制实际的零件模型//20210321新建修改：校准图的绘制是BMP的直接绘制，最好绘制在最底层
            //Draw the Model CLIs: Draw the Conrols with the universial unit: MM
            switch (g_CorrectionFigureFlag)
            {
                case 0:
                    //RenderingFromCLIMoudle(CLIImportFlag, CLIlayerIndex);

                    break;
                case 1:
                    //RenderingFromBMPFile();
                    RenderingFromBMPFile();

                    break;
                case 2:
                    break;
                case 3:
                    break;
                case 4:
                    break;
            }

            //(2)Draw base lines: 绘制基板线
            if (g_CorrectionFigureFlag == 0)//20210321：确保处于非校准图显示模式
            { 
                for (float i = 2.5f/*2*/; i < 17;/*i++*/ i = i + 2.5f/* 2*/)//绘制X轴坐标线//20200621优化：刻度值为50
                {
                    deviceContext.DrawLine(new Vector2(-10 * i * mm2Dip, 165/*175*/ * mm2Dip), new Vector2(-10 * i * mm2Dip, -165/*175*/ * mm2Dip), BaseLineBrush, 1f / (0.5f * m_zoomScale), strokeStyle2/*strokeStyle2*/);
                    deviceContext.DrawLine(new Vector2(10 * i * mm2Dip, 165/*175*/ * mm2Dip), new Vector2(10 * i * mm2Dip, -165/*175*/ * mm2Dip), BaseLineBrush, 1f / (0.5f * m_zoomScale), strokeStyle2/*strokeStyle2*/);
                }
                for (float i = 2.5f/*2*/; i < 17/*18*/; /*i++*/ i = i + 2.5f/*2*/)//绘制Y轴坐标线//20200621优化：刻度值为50
                {
                    deviceContext.DrawLine(new Vector2(-165/*210*/ * mm2Dip, -10 * i * mm2Dip), new Vector2(165/*210*/ * mm2Dip, -10 * i * mm2Dip), BaseLineBrush, 1f / (0.5f * m_zoomScale), strokeStyle2 /*strokeStyle2*/);
                    deviceContext.DrawLine(new Vector2(-165/*210*/ * mm2Dip, 10 * i * mm2Dip), new Vector2(165/*210*/ * mm2Dip, 10 * i * mm2Dip), BaseLineBrush, 1f / (0.5f * m_zoomScale), strokeStyle2 /*strokeStyle2*/);
                }
            }

            ////(3)绘制基板4个定位孔：
            //DrawPositionHole(195, 160, 10, OutlineBrush, LocationHoleBrush);
            //////CoordinatLineBrush = new SolidColorBrush(deviceContext, SharpDX.Color./*Red*/CornflowerBlue);

            //(3)绘制成型基板的的对称轴线以及基板的定位栅格——————————————————————————————————————————            
            deviceContext.DrawLine(new Vector2(-2100 * mm2Dip, 0 * mm2Dip), new Vector2(2100 * mm2Dip, 0 * mm2Dip), CoordinatLineBrush, 1f / (0.5f * m_zoomScale), strokeStyle2);
            deviceContext.DrawLine(new Vector2(0 * mm2Dip, -1750 * mm2Dip), new Vector2(0 * mm2Dip, 1750 * mm2Dip), CoordinatLineBrush, 1f / (0.5f * m_zoomScale), strokeStyle2);

            //(3-2)绘制基板的喷头对应区域
            if (g_CorrectionFigureFlag==0)//20210321：确保处于非校准图显示模式
            {
                RawColor4 rawColor4 = SharpDX.Color.Blue/*Green*//* new RawColor4(50, 50, 100, 255)*/;
                //OutlineBrush = new SolidColorBrush(deviceContext, SharpDX.Color.Black/*Red*/);
                OutlineBrush.Color = SharpDX.Color.Black/*Red*/;

                //CornerCoverBrush = new SolidColorBrush(deviceContext, rawColor4/*SharpDX.Color.LightBlue*/);//20200612新增
                CornerCoverBrush.Color = rawColor4;

                CornerCoverBrush.Opacity = 0.2f/*0.45f*/;//设置透明程度
                RawRectangleF rectangleF5 = new RawRectangleF(-165/*210*/ * mm2Dip, -75 * mm2Dip, 165/*210*/ * mm2Dip, -125 * mm2Dip);//左上右下
                //deviceContext.FillRectangle(rectangleF5, CornerCoverBrush);
                //deviceContext.DrawRectangle(rectangleF5, OutlineBrush, 1.2f, strokeStyle3);
                rectangleF5 = new RawRectangleF(-165/*210*/ * mm2Dip, 75 * mm2Dip, 165/*210*/ * mm2Dip, 125 * mm2Dip);//左上右下
                //deviceContext.FillRectangle(rectangleF5, CornerCoverBrush);
                //deviceContext.DrawRectangle(rectangleF5, OutlineBrush, 1.2f, strokeStyle3);
                rawColor4 = SharpDX.Color.Blue/*Green*//* new RawColor4(50, 50, 100, 255)*/;
                //CornerCoverBrush = new SolidColorBrush(deviceContext, rawColor4/*SharpDX.Color.LightBlue*/);//20200612新增
                CornerCoverBrush.Color = rawColor4;

                CornerCoverBrush.Opacity = 0.2f/*0.45f*/;//设置透明程度
                rectangleF5 = new RawRectangleF(-165/*210*/ * mm2Dip, -25 * mm2Dip, 165/*210*/ * mm2Dip, 25 * mm2Dip);//左上右下
                //deviceContext.FillRectangle(rectangleF5, CornerCoverBrush);
                //deviceContext.DrawRectangle(rectangleF5, OutlineBrush, 1.2f, strokeStyle3);
                rawColor4 = SharpDX.Color.Green/*Yellow*//*Green*//* new RawColor4(50, 50, 100, 255)*/;
                //CornerCoverBrush = new SolidColorBrush(deviceContext, rawColor4/*SharpDX.Color.LightBlue*/);//20200612新增
                CornerCoverBrush.Color = rawColor4;

                CornerCoverBrush.Opacity = 0.15f/*0.45f*/;//设置透明程度
                rectangleF5 = new RawRectangleF(-165/*210*/ * mm2Dip, 25 * mm2Dip, 165/*210*/ * mm2Dip, 75 * mm2Dip);//左上右下
                //deviceContext.FillRectangle(rectangleF5, CornerCoverBrush);
                //deviceContext.DrawRectangle(rectangleF5, OutlineBrush, 1.2f, strokeStyle3);
                rectangleF5 = new RawRectangleF(-165/*210*/ * mm2Dip, -25 * mm2Dip, 165/*210*/ * mm2Dip, -75 * mm2Dip);//左上右下
                //deviceContext.FillRectangle(rectangleF5, CornerCoverBrush);
                //deviceContext.DrawRectangle(rectangleF5, OutlineBrush, 1.2f, strokeStyle3);
                //OutlineBrush = new SolidColorBrush(deviceContext, SharpDX.Color.Black);
                OutlineBrush.Color = SharpDX.Color.Black;

                //CornerCoverBrush = new SolidColorBrush(deviceContext, SharpDX.Color.LightBlue);//20200612新增
                CornerCoverBrush.Color = SharpDX.Color.LightBlue;
            }
            else { }


            //(4)绘制实际的零件模型//20210321新建修改：校准图的绘制是BMP的直接绘制，最好绘制在最底层
            //Draw the Model CLIs: Draw the Conrols with the universial unit: MM
            switch (g_CorrectionFigureFlag)
            {
                case 0:
                    RenderingFromCLIMoudle(CLIImportFlag, CLIlayerIndex);
                    break;
                case 1:
                    ////RenderingFromBMPFile();
                    //RenderingFromBMPFile();

                    break;
                case 2:
                    break;
                case 3:
                    break;
                case 4:
                    break;
            }

            //(3)绘制基板4个定位孔：
            DrawPositionHole(150/*195*/, 150/*160*/, 10, OutlineBrush, LocationHoleBrush);
            ////CoordinatLineBrush = new SolidColorBrush(deviceContext, SharpDX.Color./*Red*/CornflowerBlue);


            //(5)绘制标尺背景：Draw Base contoul and back: 绘制基板轮廓背景
            deviceContext.Transform = new RawMatrix3x2(1, 0, 0, 1, 0, 0);//deviceContext.ResetTransform();
            RawColor4 rawColor41 = SharpDX.Color.Yellow/*LightGreen*/;
            //RulerBackBrush2 = new SolidColorBrush(deviceContext, rawColor41/*SharpDX.Color.LightBlue*/);//20200612新增
            RulerBackBrush2.Color = rawColor41;


            RulerBackBrush2.Opacity = 1f;//设置透明程度
            RawRectangleF rectangleF41 = new RawRectangleF(0f * mm2Dip, ysize - 2f/*27f*/, 1000f * mm2Dip, ysize/* ysize* mm2Dip*/);//左上右下//20210310修改：更加舒适，缩减25
            deviceContext.FillRectangle(rectangleF41, RulerBackBrush2);//20201109新增
            RawRectangleF rectangleF42 = new RawRectangleF(xsize - 2, 0f * mm2Dip, xsize, 800f * mm2Dip);
            deviceContext.FillRectangle(rectangleF42, RulerBackBrush2);//20201109新增
            RawRectangleF rectangleF21 = new RawRectangleF(0f * mm2Dip, 0f * mm2Dip, 1000f * mm2Dip, 4 * 2.3f * mm2Dip);//左上右下
            deviceContext.FillRectangle(rectangleF21, RulerBackBrush2);
            RawRectangleF rectangleF31 = new RawRectangleF(0f * mm2Dip, 0f * mm2Dip, 4 * 2.3f * mm2Dip, 800f * mm2Dip);//左上右下
            deviceContext.FillRectangle(rectangleF31, RulerBackBrush2);

            RawColor4 rawColor411 = SharpDX.Color.White;
            //RulerBackBrush2 = new SolidColorBrush(deviceContext, rawColor411/*SharpDX.Color.LightBlue*/);//20200612新增
            RulerBackBrush2.Color = rawColor411;



            RulerBackBrush2.Opacity = 1f;//设置透明程度
            RawRectangleF rectangleF211 = new RawRectangleF(0f * mm2Dip, 0f * mm2Dip, 1000f * mm2Dip, 4 * 2f * mm2Dip);//左上右下
            deviceContext.FillRectangle(rectangleF211, RulerBackBrush2);
            RawRectangleF rectangleF311 = new RawRectangleF(0f * mm2Dip, 0f * mm2Dip, 4 * 2f * mm2Dip, 800f * mm2Dip);//左上右下
            deviceContext.FillRectangle(rectangleF311, RulerBackBrush2);


            RulerBackBrush.Opacity = 0.25f/*0.45f*/;//设置透明程度
            RawRectangleF rectangleF2 = new RawRectangleF(0f * mm2Dip, 0f * mm2Dip, 1000f * mm2Dip, 4 * 2 * mm2Dip);//左上右下
            deviceContext.FillRectangle(rectangleF2, RulerBackBrush);
            deviceContext.DrawRectangle(rectangleF2, OutlineBrush);
            RawRectangleF rectangleF3 = new RawRectangleF(0f * mm2Dip, 0f * mm2Dip, 4 * 2 * mm2Dip, 800f * mm2Dip);//左上右下
            deviceContext.FillRectangle(rectangleF3, RulerBackBrush);
            deviceContext.DrawRectangle(rectangleF3, OutlineBrush);

            //(6)绘制标尺刻度、文字：
            Matrix3x2 myMatrix2 = new Matrix3x2(1, 0, 0, -1, 0, 0);//Y向坐标系翻转:
            myMatrix2.ScaleVector = new Vector2(0.5f, 0.5f);//本机电脑上显示比例与实际比例差别
            myMatrix2.TranslationVector = new Vector2(/*xsize / 2 + 3, ysize / 2*/viewportbase.X, viewportbase.Y);//坐标系平移
            deviceContext.Transform = myMatrix2;
            Matrix3x2 myMatrix4 = new Matrix3x2(0, -0.5f, 0.5f, 0, /*xsize / 2 + 3, ysize / 2*/viewportbase.X, viewportbase.Y);//Y向坐标系翻转:
            for (int i = 0; i <= 200; i++)//i<=100*30/mainRuler
            {
                //主刻度:X方向
                deviceContext.DrawLine(new Vector2((i * mainRuler * m_zoomScale) * mm2Dip, 19 * mm2Dip - /*(ysize / 2)*/viewportbase.Y * 2),
                    new Vector2((i * mainRuler * m_zoomScale) * mm2Dip, 13 * mm2Dip - /*(ysize / 2)*/ viewportbase.Y * 2), RulerLineBrush, 2f, strokeStyle1);
                deviceContext.DrawLine(new Vector2((-i * mainRuler * m_zoomScale) * mm2Dip, 19 * mm2Dip - /*(ysize / 2)*/ viewportbase.Y * 2),
                    new Vector2((-i * mainRuler * m_zoomScale) * mm2Dip, 13 * mm2Dip - /*(ysize / 2)*/viewportbase.Y * 2), RulerLineBrush, 2f, strokeStyle1);
                //主刻度:Y方向
                deviceContext.DrawLine(new Vector2(13f * mm2Dip - /*(xsize / 2 + 3)*/viewportbase.X * 2, (i * mainRuler * m_zoomScale) * mm2Dip),
                    new Vector2(19 * mm2Dip - /*(xsize / 2 + 3)*/viewportbase.X * 2, (i * mainRuler * m_zoomScale) * mm2Dip), RulerLineBrush, 2f, strokeStyle1);
                deviceContext.DrawLine(new Vector2(13f * mm2Dip - /*(xsize / 2 + 3)*/viewportbase.X * 2, (-i * mainRuler * m_zoomScale) * mm2Dip),
                    new Vector2(19 * mm2Dip - /*(xsize / 2 + 3)*/viewportbase.X * 2, (-i * mainRuler * m_zoomScale) * mm2Dip), RulerLineBrush, 2f, strokeStyle1);
                float PositionValue = i * mainRuler;//20200524修改非常关键：
                string str = (PositionValue).ToString("G0")/*"0"*/;
                float offset = 0;
                switch (str.Length)
                {
                    case 1:
                        offset = 2.8f;
                        break;
                    case 2:
                        offset = 5.5f;
                        break;
                    case 3:
                        offset = 9f;
                        break;
                    case 4:
                        offset = 13.5f;
                        break;
                    default:
                        break;
                }
                //主刻度文字:X方向
                deviceContext.DrawText(str, textFormat4/*new TextFormat(dwFactory, "宋体", FontWeight.UltraBold, SharpDX.DirectWrite.FontStyle.Normal, 30f)*/,
                    new RawRectangleF(PositionValue * m_zoomScale * mm2Dip - offset * mm2Dip, 2 * mm2Dip - viewportbase.Y * 2,
                    PositionValue * m_zoomScale * mm2Dip + 25 * mm2Dip, 10 * mm2Dip - viewportbase.Y * 2),
                    OutlineBrush, DrawTextOptions.None, MeasuringMode.GdiClassic/*GdiNatural*/);
                if (i > 0)
                {
                    deviceContext.DrawText("-" + str, textFormat4/*new TextFormat(dwFactory, "宋体", FontWeight.UltraBold, SharpDX.DirectWrite.FontStyle.Normal, 30f)*/,
                        new RawRectangleF(-PositionValue * m_zoomScale * mm2Dip - (offset + 4.5f) * mm2Dip, 2 * mm2Dip - viewportbase.Y * 2,
                        -PositionValue * m_zoomScale * mm2Dip + 25 * mm2Dip, 10 * mm2Dip - viewportbase.Y * 2),
                        OutlineBrush, DrawTextOptions.None, MeasuringMode.GdiClassic/*GdiNatural*/);
                }
#if true
                // save the current tranform
                var currentTransform = deviceContext.Transform;
                // set a 90 degree rotation around the (100,100);
                deviceContext.Transform = myMatrix4;
                // do your rotated text drawings
                //主刻度文字:Y方向
                deviceContext.DrawText(str, textFormat4/*new TextFormat(dwFactory, "宋体", FontWeight.UltraBold, SharpDX.DirectWrite.FontStyle.Normal, 30f)*/,
                    new RawRectangleF(PositionValue * m_zoomScale * mm2Dip + 25 * mm2Dip, +10 * mm2Dip - viewportbase.X * 2,
                    PositionValue * m_zoomScale * mm2Dip - offset * mm2Dip, +2 * mm2Dip - viewportbase.X * 2),
                    OutlineBrush, DrawTextOptions.None, MeasuringMode.GdiClassic/*GdiNatural*/);
                if (i > 0)
                {
                    deviceContext.DrawText("-" + str, textFormat4/*new TextFormat(dwFactory, "宋体", FontWeight.UltraBold, SharpDX.DirectWrite.FontStyle.Normal, 30f)*/,
                        new RawRectangleF(-PositionValue * m_zoomScale * mm2Dip + 25 * mm2Dip, +10 * mm2Dip - viewportbase.X * 2,
                        -PositionValue * m_zoomScale * mm2Dip - (offset + 4.5f) * mm2Dip, +2 * mm2Dip - viewportbase.X * 2),
                        OutlineBrush, DrawTextOptions.None, MeasuringMode.GdiClassic/*GdiNatural*/);
                }
                //restore your previous/original transform
                deviceContext.Transform = currentTransform;
#else//20200524测试：在win7系统无法使用，只能在win8及win10系统上采用
                            //deviceContext.DrawTextLayout(new RawVector2(100,100), textLayout2, solidBrush2);
                            deviceContext.DrawTextLayout(new RawVector2(-250, -200), textLayout2, solidBrush2);
#endif
            }
            //(7)绘制标尺子刻度、子文字：
            for (int i = 0; i <= 500; i++)//i<=100*30/mainRuler
            {
                //绘制主刻度
                deviceContext.DrawLine(new Vector2((i * mainRuler * m_zoomScale) / 10 * mm2Dip, 16 * mm2Dip - /*(ysize / 2) */viewportbase.Y * 2),
                    new Vector2((i * mainRuler * m_zoomScale) / 10 * mm2Dip, 13 * mm2Dip - /*(ysize / 2)*/viewportbase.Y * 2), RulerLineBrush, 2f, strokeStyle1);
                deviceContext.DrawLine(new Vector2((-i * mainRuler * m_zoomScale) / 10 * mm2Dip, 16 * mm2Dip - /*(ysize / 2) */viewportbase.Y * 2),
                    new Vector2((-i * mainRuler * m_zoomScale) / 10 * mm2Dip, 13 * mm2Dip - /*(ysize / 2)*/ viewportbase.Y * 2), RulerLineBrush, 2f, strokeStyle1);

                deviceContext.DrawLine(new Vector2(13 * mm2Dip - /*(xsize / 2 + 3)*/viewportbase.X * 2, (i * mainRuler * m_zoomScale) / 10 * mm2Dip),
                    new Vector2(16 * mm2Dip - /*(xsize / 2 + 3)*/viewportbase.X * 2, (i * mainRuler * m_zoomScale) / 10 * mm2Dip), RulerLineBrush, 2f, strokeStyle1);
                deviceContext.DrawLine(new Vector2(13 * mm2Dip - /*(xsize / 2 + 3)*/ viewportbase.X * 2, (-i * mainRuler * m_zoomScale) / 10 * mm2Dip),
                    new Vector2(16 * mm2Dip - /*(xsize / 2 + 3)*/viewportbase.X * 2, (-i * mainRuler * m_zoomScale) / 10 * mm2Dip), RulerLineBrush, 2f, strokeStyle1);
                //ruler+=mainRuler;
            }
            //(8)基本文字:X方向  
            //(a)软件版权文字
            deviceContext.Transform = new RawMatrix3x2(1, 0, 0, 1, 0, 0);//deviceContext.ResetTransform();  

            rawVector2.X = xsize - 295/*248*/; rawVector2.Y = ysize - 55/*33*//*33*//*58*/;//20210309修改：//20210310修改：修改变量值，缩减25
            deviceContext.DrawTextLayout(rawVector2/*new RawVector2(xsize - 248, ysize - 58)*/, textLayout, OutlineBrush);//deviceContext.DrawGlyphRun
            string CurrentLayer = null;                                                                                                             //(b)层数提示信息
            if (CLIImportFlag == true)
            { 
                CurrentLayer = "当前显示： " + (CLIlayerIndex+1) + "层/"+ LayerCount+ "层";
            }
            else
            {
                CurrentLayer = "当前显示： " + (CLIlayerIndex) + "层/" + LayerCount + "层";
            }
            textLayout3.Dispose();
            //(2)等待堆区释放，重新分配堆区，刷新显示
            while ((textLayout3.IsDisposed == false)) { Thread.Sleep(1); }
            textLayout3 = new TextLayout(dwFactory, CurrentLayer, textFormat3, float.PositiveInfinity, float.PositiveInfinity);//201810100309：存在内存泄漏

            rawVector2.X = xsize - 295/*248*/; rawVector2.Y = ysize - 85/*63*/;//20210309修改：
            deviceContext.DrawTextLayout(rawVector2/*new RawVector2(xsize - 248, ysize - 88)*/, textLayout3, OutlineBrush);//deviceContext.DrawGlyphRun

            //(9)绘制框选区域
            if (k_nPrepareDeleteFlag == 2)//201106新增：默认为1，等待开启选中
            {
                float x1 = k_pSelectArea[0].X;//201106新增：选中操作区域实际上是2点矩形
                float y1 = k_pSelectArea[0].Y;
                float x2 = k_pSelectArea[1].X;
                float y2 = k_pSelectArea[1].Y;

                RawRectangleF rectangleF5 = new RawRectangleF(x1/*0f*/ * 1/*mm2Dip*/, y1/*0f*/ * 1/*mm2Dip*/, x2/*4.2f*/ * 1/*mm2Dip*/, y2/*4.2f*/ * 1/*mm2Dip*/);//左上右下
                RawColor4 rawColor4 = /*SharpDX.Color.Green*/ new RawColor4(50, 50, 100, 255);
                //OutlineBrush = new SolidColorBrush(deviceContext, SharpDX.Color.Red);
                OutlineBrush.Color = SharpDX.Color.Red;

                //CornerCoverBrush = new SolidColorBrush(deviceContext, rawColor4/*SharpDX.Color.LightBlue*/);//20200612新增
                CornerCoverBrush.Color = rawColor4;

                CornerCoverBrush.Opacity = 0.5f;//设置透明程度
                deviceContext.FillRectangle(rectangleF5, CornerCoverBrush);
                deviceContext.DrawRectangle(rectangleF5, OutlineBrush,1.2f, strokeStyle3);
                //OutlineBrush = new SolidColorBrush(deviceContext, SharpDX.Color.Black);
                OutlineBrush.Color = SharpDX.Color.Black;

                //CornerCoverBrush = new SolidColorBrush(deviceContext, SharpDX.Color.LightBlue);//20200612新增
                CornerCoverBrush.Color = SharpDX.Color.LightBlue;

            }
            else if (k_nPrepareDeleteFlag == 3)
            { }
            else
            { }

            //(9)绘制标尺背景左上角盖板：Draw Base contoul and back: 绘制基板轮廓背景
            ////CornerCoverBrush = new SolidColorBrush(deviceContext, SharpDX.Color.LightBlue/*Red*//*CornflowerBlue*/);
            CornerCoverBrush.Color = SharpDX.Color.LightBlue;
            CornerCoverBrush.Opacity = 1f;//设置透明程度

            RawRectangleF rectangleF4 = new RawRectangleF(0f * mm2Dip, 0f * mm2Dip, 4.2f * 2 * mm2Dip, 4.2f * 2 * mm2Dip);//左上右下
            deviceContext.FillRectangle(rectangleF4, CornerCoverBrush);
            deviceContext.DrawRectangle(rectangleF4, OutlineBrush);
            //(10)复位世界矩阵
            deviceContext.Transform = new RawMatrix3x2(1, 0, 0, 1, 0, 0); //保证后续context操作不混乱
        }

        private void RenderingFromCLIMoudle(bool CLIImportFlag,int CLIlayerIndex)//20210306新增：封装出来
        {
            if (CLIImportFlag == true)
            {
                if (geometry != null)// check if the geometry is dirty, and disposse the old geometry
                {
                    geometry.Dispose();
                }
                //geometry = new PathGeometry(deviceContext.Factory);/*PathGeometry */
                {
#if false
                    var geometrySink = geometry.Open();
                    geometrySink.BeginFigure(new RawVector2(0 * mm2Dip, 0 * mm2Dip/*0f*/), FigureBegin.Filled/*Hollow*/);
                    geometrySink.AddLine(new RawVector2(50 * mm2Dip, 50 * mm2Dip));
                    geometrySink.AddLine(new RawVector2(50 * mm2Dip, 0 * mm2Dip));
                    geometrySink.EndFigure(FigureEnd.Closed);
                    //20200528新增测试：
                    geometrySink.BeginFigure(new RawVector2(50 * mm2Dip, 50 * mm2Dip/*0f*/), FigureBegin.Filled/*Hollow*/);
                    geometrySink.AddLine(new RawVector2(100 * mm2Dip, 100 * mm2Dip));
                    geometrySink.AddLine(new RawVector2(100 * mm2Dip, 50 * mm2Dip));
                    geometrySink.EndFigure(FigureEnd.Closed);

                    geometrySink.Close();
                    deviceContext.FillGeometry(geometry, PartSolidBrush);//核心轻量级：较接近与MetaFile的功能
                    deviceContext.DrawGeometry(geometry, OutlineBrush, 1.5f);//核心轻量级：较接近与MetaFile的功能

#else//20200528数据刷新显示
                    ///*RemoteCLIs */c_RemoteCLIs = new RemoteCLIs();//20200506新建：存放1层的所有的绘制数据
                    float x = 0; float y = 0; float deltax = 0; float deltay = 0;//x1,x2为δ，偏移值//x,y为真实位置值//1113批注：
                    //GeometrySink geometrySink =null;
                    try
                    {
                        int count = gc_RemoteCLIs[CLIlayerIndex].aLayerData.Count();
                        for (int i = 0; i < gc_RemoteCLIs[CLIlayerIndex].aLayerData.Count(); i++)//20200528新增：遍历1层的所有零件CLI
                        {
                            int ListCount = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].aLayer.LineList.Count();
                            for (int j = 0; j < ListCount; j++)//1个零件1层CLI的多条轮廓的总条数
                            {
                                x = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].x;//1个零件的单层CLI:位置X
                                y = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].y;//1个零件的单层CLI:位置Y
                                deltax = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].deltaX;//1个零件的单层CLI:位置X
                                deltay = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].deltaY;//1个零件的单层CLI:位置Y

                                int ArrayLength = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].aLayer.LineList[j].Lines[0].Length;//1个零件1层CLI的1条轮廓的点总数                                

                                if (gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].aLayer.LineList[j].dir == 0)//正向：内轮廓
                                {
                                    if (geometry != null)
                                    {
                                        geometry.Dispose();
                                    }//(1)释放内存//20210310新增修改：
                                    while (geometry.IsDisposed == false)
                                    {
                                        Thread.Sleep(1);
                                    }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：

                                    geometry = new PathGeometry(deviceContext.Factory);


                                    if (geometrySink != null)
                                    {
                                        geometrySink.Dispose();
                                    }
                                    /*var */geometrySink = geometry.Open();


                                    //geometrySink.BeginFigure(new RawVector2(0 * mm2Dip, 0 * mm2Dip), FigureBegin.Filled/*Hollow*/);//不应该放置在此处
                                    if (ArrayLength >= 3)
                                    {
                                        PointF[] TempPointF = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].aLayer.LineList[j].Lines[0].ToArray();
                                        RawVector2[] DrawPointF = null;
                                        PointF2Vector(TempPointF, ref DrawPointF, deltax/*x1*/, deltay/*y1*/);//20200528批注：内置进行了MM转换
                                        geometrySink.BeginFigure(DrawPointF[0], FigureBegin.Filled);
                                        geometrySink.AddLines(DrawPointF);
                                        //int ArrayLength = c_RemoteCLIs.aLayerData[i].aLayer.LineList[j].Lines[0].Length;//1个零件1层CLI的1条轮廓的点总数                                
                                        //直接添加一条轮廓线//for (int k = 0; k < ArrayLength; k++){ }//20200528新增：遍历一条线所有的点

                                    }
                                    else if (ArrayLength == 2)
                                    {
                                        PointF[] TempPointF = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].aLayer.LineList[j].Lines[0].ToArray();
                                        RawVector2[] DrawPointF =/* new RawVector2[ArrayLength]*/null;
                                        PointF2Vector(TempPointF, ref DrawPointF, deltax/*x1*/, deltay/*y1*/);
                                        geometrySink.BeginFigure(DrawPointF[0], FigureBegin.Filled);
                                        geometrySink.AddLines(DrawPointF);//直接添加一条轮廓线
                                    }
                                    else//ArrayLength==1
                                    {
                                        PointF[] TempPointF = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].aLayer.LineList[j].Lines[0].ToArray();
                                        RawVector2[] DrawPointF =/* new RawVector2[ArrayLength]*/null;
                                        PointF2Vector(TempPointF, ref DrawPointF, deltax/*x1*/, deltay/*y1*/);
                                        geometrySink.BeginFigure(DrawPointF[0], FigureBegin.Filled);
                                        geometrySink.AddLines(DrawPointF);//直接添加一条轮廓线
                                    }

                                    geometrySink.EndFigure(FigureEnd.Closed);//FigureEnd.Closed和FigureEnd.Open
                                    geometrySink.Close();
                                    geometrySink.Dispose();//20210310

                                    RawColor4 rawColor4 = SharpDX.Color.White;
                                    //PartSolidBrush = new SolidColorBrush(deviceContext, rawColor4);//20200612新增
                                    PartSolidBrush.Color = rawColor4;//20210310修改：非常关键
                                    deviceContext.FillGeometry(geometry, PartSolidBrush);//核心轻量级：较接近与MetaFile的功能
                                    //deviceContext.DrawGeometry(geometry, OutlineBrush, /*1.5*/0.1f);//核心轻量级：较接近与MetaFile的功能
                                    PartSolidBrush.Color = InputColorCards[7];//20200612新增

                                    geometry.Dispose();//(1)释放内存//20210310新增修改：

                                }
                                else//反向:外轮廓
                                {
                                    if (geometry != null)
                                    {
                                        geometry.Dispose();
                                    }//(1)释放内存//20210310新增修改：

                                    while (geometry.IsDisposed == false)
                                    {
                                        Thread.Sleep(1);
                                    }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：
                                    geometry = new PathGeometry(deviceContext.Factory);//20210309新建批注：此处需要完成对应的修改


                                    if (geometrySink != null)
                                    {
                                        geometrySink.Dispose();
                                    }
                                    /*var */ geometrySink = geometry.Open();
                                    //geometrySink.BeginFigure(new RawVector2(0 * mm2Dip, 0 * mm2Dip), FigureBegin.Filled/*Hollow*/);
                                    if (ArrayLength >= 3)
                                    {
                                        PointF[] TempPointF = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].aLayer.LineList[j].Lines[0].ToArray();
                                        RawVector2[] DrawPointF = null;
                                        PointF2Vector(TempPointF, ref DrawPointF, deltax/*x1*/, deltay/*y1*/);
                                        geometrySink.BeginFigure(DrawPointF[0], FigureBegin.Filled);
                                        geometrySink.AddLines(DrawPointF);
                                    }
                                    else if (ArrayLength == 2)
                                    {
                                        PointF[] TempPointF = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].aLayer.LineList[j].Lines[0].ToArray();
                                        RawVector2[] DrawPointF =/* new RawVector2[ArrayLength]*/null;
                                        PointF2Vector(TempPointF, ref DrawPointF, deltax/*x1*/, deltay/*y1*/);
                                        geometrySink.BeginFigure(DrawPointF[0], FigureBegin.Filled);
                                        geometrySink.AddLines(DrawPointF);//直接添加一条轮廓线
                                    }
                                    else//ArrayLength==1
                                    {
                                        PointF[] TempPointF = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].aLayer.LineList[j].Lines[0].ToArray();
                                        RawVector2[] DrawPointF =/* new RawVector2[ArrayLength]*/null;
                                        PointF2Vector(TempPointF, ref DrawPointF, deltax/*x1*/, deltay/*y1*/);
                                        geometrySink.BeginFigure(DrawPointF[0], FigureBegin.Filled);
                                        geometrySink.AddLines(DrawPointF);//直接添加一条轮廓线
                                    }

                                    geometrySink.EndFigure(FigureEnd.Closed);//FigureEnd.Closed和FigureEnd.Open
                                    geometrySink.Close();
                                    geometrySink.Dispose();//20210310

                                    deviceContext.FillGeometry(geometry, PartSolidBrush);//核心轻量级：较接近与MetaFile的功能
                                    //deviceContext.DrawGeometry(geometry, OutlineBrush, /*1.5*/0.2f);//核心轻量级：较接近与MetaFile的功能
                                    geometry.Dispose();//(1)释放内存//20210310新增修改：
                                }
                            }

                            int ApplaySubAreaAlthogrim = gc_RysysParam.m_bApplaySubAreaAlthogrim;//20210605新增：是否应用子区域处理算法————特别的，0是采用，1是不采用//默认采用
                            int UnactDepth = (int)gc_RysysParam.m_dUnactDepth;//20210530新增：大零件分割处理算法有效区间
                            if ((ApplaySubAreaAlthogrim == 0) && (CLIlayerIndex < UnactDepth))
                            //if (/*true*/(CLIlayerIndex <= (g_nLayerEnd - UnactDepth)) || ((g_nLayerEnd <= UnactDepth)&& CLIlayerIndex<= g_nLayerEnd))//20210122新增：大零件分割处理算法//20210530修改：
                            {
                                x = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].x;//1个零件的单层CLI:位置X
                                y = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].y;//1个零件的单层CLI:位置Y
                                float height = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].height;
                                float width = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].width;

                                float subWidth = (float)gc_RysysParam.m_dSubAreaWidth;
                                float weakWidth = (float)gc_RysysParam.m_dWeakAreaWidth / 1000;
                                float deviation = (float)gc_RysysParam.m_dDeviation;

                                List<List<PointF>> pointFs = new List<List<PointF>>();//存储1层的所有弱连接区域，每个弱连接区域以List<PointF>形式存储；
                                cLISubAeraAlgorithm.DrawWeakLine(ref pointFs, CLIlayerIndex, 1, x, y, height, width, subWidth/*4f*/, weakWidth/*0.1f*/, deviation/*2f*/);//间距4mm,层厚

                                for (int j = 0; j < pointFs.Count(); j++)
                                {
                                    //(2)添加弱连接区域到deviceContext：
                                    PointF[] TempPointF = pointFs[j].ToArray();//绘制的弱连接区域
                                    RawVector2[] DrawPointF = null;
                                    PointF2Vector3(TempPointF, ref DrawPointF, deltax, deltay);//进行坐标系变换

                                    if (geometry3 != null)
                                    {
                                        geometry3.Dispose();
                                    }//(1)释放内存//20210310新增修改：
                                    while (geometry3.IsDisposed == false)
                                    {
                                        Thread.Sleep(1);
                                    }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：

                                    /*PathGeometry*/
                                    geometry3 = new PathGeometry(deviceContext.Factory);//20210310修改：


                                    if (geometrySink != null)
                                    {
                                        geometrySink.Dispose();
                                    }
                                    /*var */geometrySink = geometry3.Open();
                                    geometrySink.BeginFigure(DrawPointF[0], FigureBegin.Filled);
                                    geometrySink.AddLines(DrawPointF);
                                    geometrySink.EndFigure(FigureEnd.Closed);//FigureEnd.Closed和FigureEnd.Open
                                    geometrySink.Close();
                                    geometrySink.Dispose();//20210310
                                    //deviceContext.FillGeometry(geometry3, PartSolidBrush);//添加一部分零件细节：核心轻量级：较接近与MetaFile的功能

                                    RawColor4 rawColor4 = SharpDX.Color.White;
                                    //PartSolidBrush = new SolidColorBrush(deviceContext, rawColor4);//20200612新增
                                    PartSolidBrush.Color = rawColor4;//20210310修改：非常关键
                                    deviceContext.FillGeometry(geometry3, PartSolidBrush);//添加一部分零件细节：核心轻量级：较接近与MetaFile的功能
                                    PartSolidBrush.Color = InputColorCards[7];//20200612新增

                                    geometry3.Dispose();//(1)释放内存//20210310新增修改：

                                }
                            }

                            if (k_nPrepareDeleteFlag == 2 || k_nPrepareDeleteFlag == 3)//20201109新增：显示半透明的方框，进入待编辑状态
                            {
                                if (k_nPrepareDeleteFlag == 2)
                                {
                                    borderx1 = (k_pSelectArea[0].X - viewportbase.X) * mm2Dip / m_zoomScale * 0.25f/*0.25f */- 1;//201106新增：选中操作区域实际上是2点矩形;其中，1为选择余量
                                    bordery1 = (k_pSelectArea[0].Y - viewportbase.Y) * mm2Dip / m_zoomScale * 0.25f/*0.25f*/ + 1;
                                    borderx2 = (k_pSelectArea[1].X - viewportbase.X) * mm2Dip / m_zoomScale * 0.25f/*0.25f */- 1;
                                    bordery2 = (k_pSelectArea[1].Y - viewportbase.Y) * mm2Dip / m_zoomScale * 0.25f/*0.25f*/ + 1;
                                }
                                float x1 = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].x - 165/*210*/;//201106新增：选中操作区域实际上是2点矩形
                                float y1 = gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].y - 165/*175*/;
                                float x2 = x1 + gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].width;
                                float y2 = y1 + gc_RemoteCLIs[CLIlayerIndex].aLayerData[i].height;
                                if ((x1 > borderx1) && (x2 < borderx2) && (y1 > bordery1) && (y2 < bordery2))
                                {
                                    RawColor4 rawColor4 = SharpDX.Color.Green/*LightGreen*//*Green*//*OrangeRed*//*Green*//*OrangeRed*//*Green*//* new RawColor4(50, 50, 100, 255)*/;
                                    //OutlineBrush = new SolidColorBrush(deviceContext, SharpDX.Color.Red);
                                    OutlineBrush.Color = SharpDX.Color.Red;//20210310修改：非常关键
                                    //CornerCoverBrush = new SolidColorBrush(deviceContext, rawColor4/*SharpDX.Color.LightBlue*/);//20200612新增
                                    CornerCoverBrush.Color = rawColor4;//20210310修改：非常关键
                                    CornerCoverBrush.Opacity = 0.75f;//设置透明程度

                                    RawRectangleF rectangleF5 = new RawRectangleF(x1 * mm2Dip, y1 * mm2Dip, x2 * mm2Dip, y2 * mm2Dip);//左上右下
                                    deviceContext.FillRectangle(rectangleF5, CornerCoverBrush);
                                    deviceContext.DrawRectangle(rectangleF5, OutlineBrush, 1.2f, strokeStyle3);
                                    //OutlineBrush = new SolidColorBrush(deviceContext, SharpDX.Color.Black);
                                    OutlineBrush.Color = SharpDX.Color.Black;//20210310修改：非常关键
                                    //CornerCoverBrush = new SolidColorBrush(deviceContext, SharpDX.Color.LightBlue);//20200612新增 
                                    CornerCoverBrush.Color = SharpDX.Color.LightBlue;//20210310修改：非常关键
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        CLIImportFlag = false;
                    }
#endif
                }

                //selectPaths
                if (k_nPrepareDeleteFlag == 2 || k_nPrepareDeleteFlag == 3)//20201110新增：记录选中零件的路径
                {
                    if (k_nPrepareDeleteFlag == 2)
                    {
                        borderx1 = (k_pSelectArea[0].X - viewportbase.X) * mm2Dip / m_zoomScale * 0.25f - 1;//201106新增：选中操作区域实际上是2点矩形;其中，1为选择余量
                        bordery1 = (k_pSelectArea[0].Y - viewportbase.Y) * mm2Dip / m_zoomScale * 0.25f + 1;
                        borderx2 = (k_pSelectArea[1].X - viewportbase.X) * mm2Dip / m_zoomScale * 0.25f - 1;
                        bordery2 = (k_pSelectArea[1].Y - viewportbase.Y) * mm2Dip / m_zoomScale * 0.25f + 1;
                    }
                    selectPaths.Clear();
                    for (int i = 0; i < tempJobItems.Count(); i++)//遍历所有的CAD数据
                    {
                        float x = (float)tempJobItems[i].position.X;
                        float y = (float)tempJobItems[i].position.Y;
                        float width = (float)tempJobItems[i].Width;//20201108新增
                        float height = (float)tempJobItems[i].Height;//20201108新增

                        float x1 = x - 165/*210*/;
                        float y1 = y - 165/*175*/;
                        float x2 = x1 + width;
                        float y2 = y1 + height;
                        if ((x1 > borderx1) && (x2 < borderx2) && (y1 > bordery1) && (y2 < bordery2))
                        {
                            selectPaths.Add(tempJobItems[i].Path);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 20200609：
        /// </summary>
        ImagingFactory wicFactory;
        public Bitmap wicBitmap;//2020118新增：此处会造成内存泄漏，添加1层保护
        WicRenderTarget d2dRenderTarget;
        SolidColorBrush solidColorBrush, solidColorBrush2;
        SharpDX.Direct2D1.Factory d2dFactory;
        /// <summary>
        /// 
        /// </summary>
        float mm2Dip2 = 1 / 25.4f * 96f;
        /// <summary>
        /// 20200610批注：用于数据处理及传送的坐标变换公式
        /// </summary>
        /// <param name="TempPointF"></param>
        /// <param name="DrawPointF"></param>
        private void PointF2Vector2(PointF[] TempPointF, ref RawVector2[] DrawPointF, float deltax, float deltay) //20200528新建测试：
        {
            DrawPointF = new RawVector2[TempPointF.Length];
            for (int count = 0; count < TempPointF.Length; count++)
            {
                DrawPointF[count].X = (TempPointF[count].X - 165/*210*/ + deltax) * mm2Dip2;//20201113修改：非常关键，实际打印应该问题不大
                DrawPointF[count].Y = (TempPointF[count].Y - 165/*175*/ + deltay) * mm2Dip2;//20201113修改：非常关键，实际打印应该问题不大
            }
        }
        private void PointF2Vector(PointF[] TempPointF, ref RawVector2[] DrawPointF, float deltax, float deltay) //20200528新建测试：
        {
            DrawPointF = new RawVector2[TempPointF.Length];
            for (int count = 0; count < TempPointF.Length; count++)
            {
                DrawPointF[count].X = (TempPointF[count].X - 165/*210*/ + deltax) * mm2Dip;//20201112修改：
                DrawPointF[count].Y = (TempPointF[count].Y - 165/*175*/ + deltay) * mm2Dip;//20201112修改：
            }
        }
        private void PointF2Vector3(PointF[] TempPointF, ref RawVector2[] DrawPointF, float deltax, float deltay) //20210113新建测试：
        {
            DrawPointF = new RawVector2[TempPointF.Length];
            for (int count = 0; count < TempPointF.Length; count++)
            {
                DrawPointF[count].X = (TempPointF[count].X - 165/*210*/ + 0) * mm2Dip;//20201112修改：
                DrawPointF[count].Y = (TempPointF[count].Y - 165/*175*/ + 0) * mm2Dip;//20201112修改：
            }
        }
        private void PointF2Vector32(PointF[] TempPointF, ref RawVector2[] DrawPointF, float deltax, float deltay) //20210113新建测试：
        {
            DrawPointF = new RawVector2[TempPointF.Length];
            for (int count = 0; count < TempPointF.Length; count++)
            {
                DrawPointF[count].X = (TempPointF[count].X - 165/*210*/ + 0) * mm2Dip2;//20201112修改：
                DrawPointF[count].Y = (TempPointF[count].Y - 165/*175*/ + 0) * mm2Dip2;//20201112修改：
            }
        }
        PathGeometry geometry2;//20200610新增：
        /// <summary>
        /// 20200609：
        /// </summary>
        /// <param name="CLIImportFlag"></param>
        /// <param name="CLIlayerIndex"></param>
        /// <param name="xsize"></param>
        /// <param name="ysize"></param>
        ///  //20230317修改：修复中断打印之后，重新启动设置新区间，打印过程中的实际传输实际仍然按照第1层数据发送的BUG
        public void DrawLayerBMP(bool CLIImportFlag, int InputCLIlayerIndex, float xsize, float ysize, SolidColorBrush solidColorBrush,int ActualStartNum)
        {
#if true //20230317修改：修复中断打印之后，重新启动设置新区间，打印过程中的实际传输实际仍然按照第1层数据发送的BUG
            int CLIlayerIndex = InputCLIlayerIndex/*CLIlayerIndex*/ + (ActualStartNum/*-1*/);
#endif

            ////d2dRenderTarget.UnitMode = UnitMode.Dips;//wicRenderTarget的绘图方式应该是像素
            //Size2F size2F = new Size2F(96f/*96 * 2f*/, 96f/* 96 * 2f*/);//设备无关像素单位设置：≈=物理单位设置
            //d2dRenderTarget.DotsPerInch = size2F;
            ////d2dRenderTarget.AntialiasMode = AntialiasMode./*PerPrimitive*/PerPrimitive/*Aliased*/;//抗锯齿模式

            ////(1)Draw Base contoul and back: 绘制基板轮廓背景          
            //Matrix3x2 myMatrix = new Matrix3x2(1, 0, 0, 1, 0, 0);//Y向坐标系翻转://世界坐标系坐标系变换：单位像素，全局变换
            //Vector2 vector1 = new Vector2(600 / 96f/*1*//*0.5f * m_zoomScale*/, 600 / 96f/*1*//*0.5f * m_zoomScale*/);//非常关键：20200524新增
            //myMatrix.ScaleVector = vector1;//本机电脑上显示比例与实际比例差别
            //Vector2 vector2 = new Vector2(xsize / 2/*viewportbase.X*/, ysize / 2/*viewportbase.Y*/);//非常关键：20200524新增
            //myMatrix.TranslationVector = vector2;//坐标系平移
            //d2dRenderTarget.Transform = myMatrix;

            //(4)绘制实际的零件模型
            //Draw the Model CLIs: Draw the Conrols with the universial unit: MM
            if (CLIImportFlag == true)
            {
                if (geometry2 != null)// check if the geometry is dirty, and disposse the old geometry
                {
                    geometry2.Dispose();
                }
                //geometry = new PathGeometry(d2dRenderTarget.Factory);/*PathGeometry */
                {
#if true            //20200528数据刷新显示
                    ///*RemoteCLIs */c_RemoteCLIs = new RemoteCLIs();//20200506新建：存放1层的所有的绘制数据
                    float x = 0; float y = 0; float deltax = 0; float deltay = 0;//x1,x2为δ，偏移值//x,y为真实位置值//1113批注：
                    //GeometrySink geometrySink =null;
                    for (int i = 0; i < gc_RemoteCLIs2[CLIlayerIndex].aLayerData.Count(); i++)//20200528新增：遍历1层的所有零件CLI
                    {
                        int ListCount = gc_RemoteCLIs2[CLIlayerIndex].aLayerData[i].aLayer.LineList.Count();
                        for (int j = 0; j < ListCount; j++)//1个零件1层CLI的多条轮廓的总条数
                        {
                            x = gc_RemoteCLIs2[CLIlayerIndex].aLayerData[i].x;//1个零件的单层CLI:位置X
                            y = gc_RemoteCLIs2[CLIlayerIndex].aLayerData[i].y;//1个零件的单层CLI:位置Y
                            deltax = gc_RemoteCLIs2[CLIlayerIndex].aLayerData[i].deltaX;//1个零件的单层CLI:位置X
                            deltay = gc_RemoteCLIs2[CLIlayerIndex].aLayerData[i].deltaY;//1个零件的单层CLI:位置Y

                            int ArrayLength = gc_RemoteCLIs2[CLIlayerIndex].aLayerData[i].aLayer.LineList[j].Lines[0].Length;//1个零件1层CLI的1条轮廓的点总数                                

                            if (gc_RemoteCLIs2[CLIlayerIndex].aLayerData[i].aLayer.LineList[j].dir == 0)//正向//内轮廓
                            {
                                geometry2 = new PathGeometry(/*d2dRenderTarget.Factory*/d2dFactory);
                                var geometrySink = geometry2.Open();
                                //geometrySink.BeginFigure(new RawVector2(0 * mm2Dip, 0 * mm2Dip), FigureBegin.Filled/*Hollow*/);//不应该放置在此处
                                if (ArrayLength >= 3)
                                {
                                    PointF[] TempPointF = gc_RemoteCLIs2[CLIlayerIndex].aLayerData[i].aLayer.LineList[j].Lines[0].ToArray();
                                    RawVector2[] DrawPointF = null;
                                    PointF2Vector2(TempPointF, ref DrawPointF, deltax/*x1*/, deltay/*y1*/);//20200528批注：内置进行了MM转换//20201113修改
                                    geometrySink.BeginFigure(DrawPointF[0], FigureBegin.Filled);
                                    geometrySink.AddLines(DrawPointF);
                                    //int ArrayLength = c_RemoteCLIs.aLayerData[i].aLayer.LineList[j].Lines[0].Length;//1个零件1层CLI的1条轮廓的点总数                                
                                    //直接添加一条轮廓线//for (int k = 0; k < ArrayLength; k++){ }//20200528新增：遍历一条线所有的点
                                }
                                else if (ArrayLength == 2)
                                {
                                    PointF[] TempPointF = gc_RemoteCLIs2[CLIlayerIndex].aLayerData[i].aLayer.LineList[j].Lines[0].ToArray();
                                    RawVector2[] DrawPointF =/* new RawVector2[ArrayLength]*/null;
                                    PointF2Vector2(TempPointF, ref DrawPointF, deltax/*x1*/, deltay/*y1*/);//20201113修改
                                    geometrySink.BeginFigure(DrawPointF[0], FigureBegin.Filled);
                                    geometrySink.AddLines(DrawPointF);//直接添加一条轮廓线
                                }
                                else//ArrayLength==1
                                {
                                    PointF[] TempPointF = gc_RemoteCLIs2[CLIlayerIndex].aLayerData[i].aLayer.LineList[j].Lines[0].ToArray();
                                    RawVector2[] DrawPointF =/* new RawVector2[ArrayLength]*/null;
                                    PointF2Vector2(TempPointF, ref DrawPointF, deltax/*x1*/, deltay/*y1*/);//20201113修改
                                    geometrySink.BeginFigure(DrawPointF[0], FigureBegin.Filled);
                                    geometrySink.AddLines(DrawPointF);//直接添加一条轮廓线
                                }

                                geometrySink.EndFigure(FigureEnd.Closed);//FigureEnd.Closed和FigureEnd.Open
                                geometrySink.Close();
                                d2dRenderTarget.FillGeometry(geometry2, solidColorBrush2 );//核心轻量级：较接近与MetaFile的功能
                                //d2dRenderTarget.DrawGeometry(geometry2, solidColorBrush, 1.5f);//核心轻量级：较接近与MetaFile的功能//消除极限尺寸误差
                            }
                            else//反向//外轮廓
                            {
                                geometry2 = new PathGeometry(/*d2dRenderTarget.Factory*/d2dFactory);
                                var geometrySink = geometry2.Open();
                                //geometrySink.BeginFigure(new RawVector2(0 * mm2Dip, 0 * mm2Dip), FigureBegin.Filled/*Hollow*/);
                                if (ArrayLength >= 3)
                                {
                                    PointF[] TempPointF = gc_RemoteCLIs2[CLIlayerIndex].aLayerData[i].aLayer.LineList[j].Lines[0].ToArray();
                                    RawVector2[] DrawPointF = null;
                                    PointF2Vector2(TempPointF, ref DrawPointF, deltax/*x1*/, deltay/*y1*/);//20201113修改
                                    geometrySink.BeginFigure(DrawPointF[0], FigureBegin.Filled);
                                    geometrySink.AddLines(DrawPointF);
                                }
                                else if (ArrayLength == 2)
                                {
                                    PointF[] TempPointF = gc_RemoteCLIs2[CLIlayerIndex].aLayerData[i].aLayer.LineList[j].Lines[0].ToArray();
                                    RawVector2[] DrawPointF =/* new RawVector2[ArrayLength]*/null;
                                    PointF2Vector2(TempPointF, ref DrawPointF, deltax/*x1*/, deltay/*y1*/);//20201113修改
                                    geometrySink.BeginFigure(DrawPointF[0], FigureBegin.Filled);
                                    geometrySink.AddLines(DrawPointF);//直接添加一条轮廓线
                                }
                                else//ArrayLength==1
                                {
                                    PointF[] TempPointF = gc_RemoteCLIs2[CLIlayerIndex].aLayerData[i].aLayer.LineList[j].Lines[0].ToArray();
                                    RawVector2[] DrawPointF =/* new RawVector2[ArrayLength]*/null;
                                    PointF2Vector2(TempPointF, ref DrawPointF, deltax/*x1*/, deltay/*y1*/);//20201113修改
                                    geometrySink.BeginFigure(DrawPointF[0], FigureBegin.Filled);
                                    geometrySink.AddLines(DrawPointF);//直接添加一条轮廓线
                                }

                                geometrySink.EndFigure(FigureEnd.Closed);//FigureEnd.Closed和FigureEnd.Open
                                geometrySink.Close();
                                d2dRenderTarget.FillGeometry(geometry2, solidColorBrush);//核心轻量级：较接近与MetaFile的功能
                                //d2dRenderTarget.DrawGeometry(geometry2, solidColorBrush , 1.5f);//核心轻量级：较接近与MetaFile的功能//消除极限尺寸误差
                            }
                        }

                        int ApplaySubAreaAlthogrim = gc_RysysParam.m_bApplaySubAreaAlthogrim;//20210605新增：是否应用子区域处理算法————特别的，0是采用，1是不采用//默认采用
                        int UnactDepth = (int)gc_RysysParam.m_dUnactDepth;//20210530新增：大零件分割处理算法有效区间

                        if ((ApplaySubAreaAlthogrim == 0) &&(CLIlayerIndex < UnactDepth))
                        //if (/*true*/(CLIlayerIndex<= (g_nLayerEnd-UnactDepth))|| ((g_nLayerEnd <= UnactDepth) && CLIlayerIndex <= g_nLayerEnd))//20210122新增：大零件分割处理算法：生成分割之后的打印数据//20210530修改：
                        {
                            x = gc_RemoteCLIs2[CLIlayerIndex].aLayerData[i].x;//1个零件的单层CLI:位置X
                            y = gc_RemoteCLIs2[CLIlayerIndex].aLayerData[i].y;//1个零件的单层CLI:位置Y
                            float height = gc_RemoteCLIs2[CLIlayerIndex].aLayerData[i].height;
                            float width = gc_RemoteCLIs2[CLIlayerIndex].aLayerData[i].width;

                            float subWidth  = (float)gc_RysysParam.m_dSubAreaWidth;
                            float weakWidth = (float)gc_RysysParam.m_dWeakAreaWidth/1000;
                            float deviation = (float)gc_RysysParam.m_dDeviation;

                            List<List<PointF>> pointFs = new List<List<PointF>>();//存储1层的所有弱连接区域，每个弱连接区域以List<PointF>形式存储；
                            cLISubAeraAlgorithm.DrawWeakLine(ref pointFs, CLIlayerIndex, 1, x, y, height, width, subWidth/*4f*/, weakWidth/*0.1f*/, deviation/*2f*/);//间距4mm,层厚

                            for (int j = 0; j < pointFs.Count(); j++)
                            {
                                //(2)添加弱连接区域到deviceContext：
                                PointF[] TempPointF = pointFs[j].ToArray();//绘制的弱连接区域
                                RawVector2[] DrawPointF = null;
                                PointF2Vector32(TempPointF, ref DrawPointF, deltax, deltay);//进行坐标系变换
                                PathGeometry geometry = new PathGeometry(d2dRenderTarget.Factory);
                                var geometrySink = geometry.Open();
                                geometrySink.BeginFigure(DrawPointF[0], FigureBegin.Filled);
                                geometrySink.AddLines(DrawPointF);
                                geometrySink.EndFigure(FigureEnd.Closed);//FigureEnd.Closed和FigureEnd.Open
                                geometrySink.Close();

                                d2dRenderTarget.FillGeometry(geometry, solidColorBrush2);//添加一部分零件细节：核心轻量级：较接近与MetaFile的功能
                            }
                        }
                    }
#endif
                }
            }
        }
        public int g_nLayerEnd = 10;//20210530新增：打印的总层数：打印显示的时候都会即时更新

        /// <summary>
        /// 20200609：
        /// </summary>
        public void InitRenderToWic()//在主界面调用
        {
            wicFactory = new ImagingFactory();
            d2dFactory = new SharpDX.Direct2D1.Factory();
        }
        /// <summary>
        /// 20200609:析构数据处理资源
        /// </summary>
        public void DestructRenderToWic()
        {
            if (wicFactory != null)
            {
                wicFactory.Dispose();
            }
            if (wicBitmap!=null)
            {
                wicBitmap.Dispose();
            }
            if (d2dRenderTarget != null)
            {
                d2dRenderTarget.Dispose();
            }
            if (solidColorBrush != null)
            {
                solidColorBrush.Dispose();
            }
            if (solidColorBrush2 != null)
            {
                solidColorBrush2.Dispose();
            }
            if (d2dFactory != null)
            {
                d2dFactory.Dispose();
            }
            if (geometry2 != null)
            {
                geometry2.Dispose();
            }
            if (geometry3 != null)
            {
                geometry3.Dispose();
            }
            if (geometrySink != null)//20210310新增：
            {
                geometrySink.Dispose();
            }

            if (fileStream != null)//20210323新增：
            {
                fileStream.Dispose();
            }
            if (bitmap != null)//20210323新增：
            {
                bitmap.Dispose();
            }
            if (bitmapDecoder != null)//20210323新增：
            {
                bitmapDecoder.Dispose();
            }
            if (frame != null)//20210323新增：
            {
                frame.Dispose();
            }
            if (imagingFactory != null)//20210323新增：
            {
                imagingFactory.Dispose();
            }
            if (converter != null)//20210323新增：
            {
                converter.Dispose();
            }
        }
        public  int XDpi = 635 * 2;//20201017新增批注：X向打印分辨率
        /// <summary>
        /// 20200609：生成1帧的加工数据//20220524新增:新设备 幅面330MM*330MM
        /// </summary>
        /// <param name="action"></param>
        public void RenderToWic(bool action,int index,int subindex,int RePrintTimes,int ActualStartNum)//subindex:重喷索引，取值为0-1-2-3-....-n//201030新增：//20230317修改：修复中断打印之后，重新启动设置新区间，打印过程中的实际传输实际仍然按照第1层数据发送的BUG
        {
            if (action == true)
            {
                //wicFactory = new ImagingFactory();
                //d2dFactory = new SharpDX.Direct2D1.Factory();

                /*const*/ int width = (int)(320/*330*//*420*/ * (/*600*//*635*//*1270*2*//*635*/XDpi / 25.4f)) + 1/*512*/;//设置图片的宽度//20200802批注：修改原有的X向分辨率，本来应该是635DPI，提升到635*2DPI。
                /*const*/ int height = (int)(320/*330*//*350*/ * (600 / 25.4f)) + 1/*512*/;//设置图片的长度

                var rectangleGeometry = new RoundedRectangleGeometry(d2dFactory, new RoundedRectangle() { RadiusX = 32, RadiusY = 32, Rect = new SharpDX.RectangleF(128, 128, width - 128 * 2, height - 128 * 2) });
                //if (wicBitmap != null)
                //{
                //    wicBitmap.Dispose();//20201118新增：防止wicBitmap出现问题//在线程删除位置处，添加了保护
                //}                
                wicBitmap = new Bitmap(wicFactory, width, height, SharpDX.WIC.PixelFormat./*Format32bppPBGRA*//*Format1bppIndexed*/Format32bppBGR, BitmapCreateCacheOption.CacheOnLoad);
                wicBitmap.SetResolution(XDpi/*635*//*1270*2*/, 600);//设置分辨率为600dpi*9600dpi


                var renderTargetProperties = new RenderTargetProperties(RenderTargetType.Default, new PixelFormat(Format.Unknown, AlphaMode.Unknown), 0, 0, RenderTargetUsage.None, SharpDX.Direct2D1.FeatureLevel.Level_DEFAULT);
                d2dRenderTarget = new WicRenderTarget(d2dFactory, wicBitmap, renderTargetProperties);
                solidColorBrush = new SolidColorBrush(d2dRenderTarget, SharpDX.Color.Black/*White*/);//修改为SharpDX的色彩
                solidColorBrush2 = new SolidColorBrush(d2dRenderTarget, SharpDX.Color.White/*White*/);//20201209新增：修改为SharpDX的色彩

                d2dRenderTarget.AntialiasMode = AntialiasMode.PerPrimitive;//开启图形的抗锯齿模式：

                //d2dRenderTarget.UnitMode = UnitMode.Dips;//wicRenderTarget的绘图方式应该是像素
                Size2F size2F = new Size2F(96f/*96 * 2f*/, 96f/* 96 * 2f*/);//设备无关像素单位设置：≈=物理单位设置
                d2dRenderTarget.DotsPerInch = size2F;
                //(1)Draw Base contoul and back: 绘制基板轮廓背景          
                Matrix3x2 myMatrix = new Matrix3x2(1, 0, 0, 1, 0, 0);//Y向坐标系翻转://世界坐标系坐标系变换：单位像素，全局变换
                Vector2 vector1 = new Vector2(XDpi/*635*//*1270*2*//*600*/ / 96f/*1*//*0.5f * m_zoomScale*/, -600 / 96f/*1*//*0.5f * m_zoomScale*/);//非常关键：20200524新增
                myMatrix.ScaleVector = vector1;//本机电脑上显示比例与实际比例差别
                Vector2 vector2 = new Vector2(width / 2/*viewportbase.X*/, height / 2/*viewportbase.Y*/);//非常关键：20200524新增
                myMatrix.TranslationVector = vector2;//坐标系平移
                d2dRenderTarget.Transform = myMatrix;

                d2dRenderTarget.BeginDraw();
                d2dRenderTarget.Clear(SharpDX.Color.White);//修改为SharpDX的色彩
#if false//测试
                d2dRenderTarget.FillGeometry(rectangleGeometry, solidColorBrush, null);
#else
                //20230317修改：修复中断打印之后，重新启动设置新区间，打印过程中的实际传输实际仍然按照第1层数据发送的BUG
                string msg = $"完成处理第{index}层数据：准备调用DrawLayerBMP，index为{index}，起始层为{ActualStartNum}";
                Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
                DrawLayerBMP(true,index,width, height,solidColorBrush,ActualStartNum);//20201117批注：生成正式打印数据
                msg = $"完成处理第{index}层数据：准备调用DrawLayerBMP，index为{index}，起始层为{ActualStartNum}";
                Log4Net.Info(msg);//20230317新建：解决20230314打印94层中途停止的潜在问题
#endif

                long tag1, tag2;
                Result EndDrawResult = d2dRenderTarget.TryEndDraw(out tag1, out tag2);//deviceContext.EndDraw(); //long a, b;deviceContext.TryEndDraw(out a,out b);
                while (EndDrawResult != Result.Ok)
                {
                    Thread.Sleep(1);//等待20ms
                    EndDrawResult = d2dRenderTarget.TryEndDraw(out tag1, out tag2);
                }

#if false
                //(1)方式1：采用文件流
                var stream = new WICStream(wicFactory, filename, NativeFileAccess.Write);//存在3种流可以选择：文件流、非托管区内存流、托管区内存流
#else
                int size2 = 54 + (wicBitmap.Size.Width) * (wicBitmap.Size.Height) * 4;//这么多字节
                IntPtr ImgPtr = Marshal.AllocHGlobal(size2);//非托管区位置
                //Marshal.Copy(rgbValues, 0, ImgPtr, size2);//复制到非托管区内存

                var stream = new WICStream(wicFactory, new DataPointer(ImgPtr, size2));//WIC流到非托管区内存
#endif
                // Initialize a Jpeg encoder with this stream
                var encoder = new BmpBitmapEncoder(wicFactory);//生成BMP图片
                encoder.Initialize(stream);

                // Create a Frame encoder
                var bitmapFrameEncode = new BitmapFrameEncode(encoder);
                bitmapFrameEncode.Initialize();
                bitmapFrameEncode.SetSize(width, height);
#if true
                var pixelFormatGuid = SharpDX.WIC.PixelFormat./*Format32bppPBGRA*/Format32bppBGR/*Format1bppIndexed*//*FormatDontCare*/;
#else
                var pixelFormatGuid = SharpDX.WIC.PixelFormat.Format1bppIndexed/*FormatDontCare*/;
#endif
                bitmapFrameEncode.SetPixelFormat(ref pixelFormatGuid);
                bitmapFrameEncode.WriteSource(wicBitmap);
                //这一步非常费时，直接编码生成Format1bppIndexed；
                //编码到Format1bppIndexed的内存流：耗时2325-2405-2640ms;（使用WIC进行格式转换）
                //编码到Format32bppBGR的内存流：耗时294-304ms;（不使用WIC进行格式转换）
                //编码到Format32bppBGR的文件流：耗时3940-4820ms;（不使用WIC进行格式转换）
                //编码到Format1bppIndexed的文件流：耗时2409-2465-2497ms;（不使用WIC进行格式转换）

                bitmapFrameEncode.Commit();//帧提交
                encoder.Commit();//编码器提交

                bitmapFrameEncode.Dispose();
                encoder.Dispose();
                stream.Dispose();
                d2dRenderTarget.Dispose();
                solidColorBrush.Dispose();
                solidColorBrush2.Dispose();//20201209新增：
                wicBitmap.Dispose();

#if false //直接从wic复制到System.Drawing.Bitamp
                var pixelData = new byte[width * height * 4];
                wicBitmap.CopyPixels(pixelData, width * 4);
                var bmp = new System.Drawing.Bitmap(width, height);
                var bd = bmp.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                Marshal.Copy(pixelData, 0, bd.Scan0, pixelData.Length);
                bmp.UnlockBits(bd);
#endif
                //从内存流解析
                //System.Drawing.Bitmap outputBMP = new System.Drawing.Bitmap(memoryStream);//20200411新增：200ms执行时间
                System.Drawing.Bitmap outputBMP = new System.Drawing.Bitmap(
                    width, height, width * 4, System.Drawing.Imaging.PixelFormat.Format32bppArgb, ImgPtr + 54);//20200609新增：直接使用非托管内存，200ms执行时间

                //耗时1000ms
                System.Drawing.Bitmap clone = outputBMP.Clone(new System.Drawing.Rectangle(0, 0, outputBMP.Width, outputBMP.Height), System.Drawing.Imaging.PixelFormat.Format1bppIndexed);
                outputBMP.Dispose();//————————————————————————释放bmp文件

                Marshal.FreeHGlobal(ImgPtr);//释放内存
                clone.SetResolution(600f, 600f);//Windows7的系统BUG

                //#region//20230202新建：根据1bpp,2bpp,3bpp++以及GrayScale来重新编码为最新需要下发的数据
                //int bpp = 1;
                //if (bpp==1)//1bpp模式
                //{

                //}
                //else if (bpp == 2) //2bpp模式下的3个灰度等级（灰阶）
                //{ }
                //else if (bpp == 3) //3bpp模式下的7个灰度等级（灰阶）
                //{ }
                //else { }
                //#endregion

#if false//20200610测试：测试生成的图片是否正确//20201118新增：方便调试
                clone.Save("output1bpp.bmp", ImageFormat.Bmp);//保存到BMPFile
#else
                WriteImgLayerData(clone/*path*/, /*1*/index,subindex, RePrintTimes/*, 0, true*/,true);//201030修改：//20201117批注：数据封送处理//20210324:需要执行反色
#endif
                //System.Diagnostics.Process.Start(Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, filename)));//打开文件夹的指定文件
                clone.Dispose();
            }
            else
            {
            }
        }

        //20210319新增：处理校准图并传送，已经验证通过
        public void RenderToWic2(bool action, int index, int subindex, int RePrintTimes, string importCorrectionFigurePath)//subindex:重喷索引，取值为0-1-2-3-....-n//201030新增：
        {
            if (action == true)
            {
                ///(1-1)设置1帧打印数据的基本参数：20210319新增
                ///(2-1)绘制1帧需要打印的数据：20210319新增
                ///(3-1)输出绘制的1帧数据，发送到控制器的上位机端内存缓冲区，配合控制器完成信息的实时打印机分配：20210319新建             
                // （1）读取BMP文件到Bitmap数据中
                string CalibrationFilePath = System.Windows.Forms.Application.StartupPath + @"\CalibrationChart" + importCorrectionFigurePath/*System.Windows.Forms.Application.StartupPath + @"\CalibrationChart"*/;//输入的CLI文件的存放目录。
                FileStream fs = new System.IO.FileStream(CalibrationFilePath/*CalibrationFilePath + @"\喷头套色校准图-0.bmp"*/, FileMode.Open, FileAccess.Read/*Read*/);//PicBoxCorrect1.Image = System.Drawing.Image.FromStream(fs);//20210328修改：修改权限，否则报错
                System.Drawing.Bitmap clone = (System.Drawing.Bitmap)System.Drawing.Bitmap.FromStream(fs);
                clone.SetResolution(600f, 600f);//Windows7的系统BUG

                ////// （2）processedBit// Lock the bitmap's bits.  map.LockBits();//锁定到内存
                ////System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, 10496/*clone.Width*/, 8960/*clone.Height*/);
                ////System.Drawing.Imaging.BitmapData bmpData = clone.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, clone.PixelFormat);
                ////// （3）Get the address of the first line.
                ////IntPtr ptr = bmpData.Scan0;

#if false//20200610测试：测试生成的图片是否正确//20201118新增：方便调试
                clone.save("output1bpp.bmp", imageformat.bmp);//保存到bmpfile
#else
                WriteImgLayerData(clone, index/*1-2-3*/, subindex/*0-1-2*/, RePrintTimes/*1*/, false);//201030修改：//20201117批注：数据封送处理//20210324:不需要执行反色
#endif
                clone.Dispose();
            }
            else { }
        }

        public Bitmap1 bitmap = null;
        public WICStream fileStream = null;
        public BitmapDecoder bitmapDecoder = null;
        public BitmapFrameDecode frame = null;
        public ImagingFactory imagingFactory = null;
        public FormatConverter converter = null;
        public Bitmap1[] bitmapCollection = new Bitmap1[6];//20210323新建：将bitmap的数据移植到bitmapCollection中，依次是
                                                           //垂直校准图，喷头套色校准图0-1，往返差校准图0-1，STATUS图
        public void LoadingFromBMPFile2(string bmpFilePath,int index)//index为校准图对应的索引
        {           
            //(1)读取校准图bmp文件
            if (imagingFactory != null)
            {
                imagingFactory.Dispose();
                while (imagingFactory.IsDisposed == false) { Thread.Sleep(1); }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：
                imagingFactory = new ImagingFactory();
            }
            else { imagingFactory = new ImagingFactory(); }

            if (fileStream != null)
            {
                fileStream.Dispose();
                while (fileStream.IsDisposed == false) { Thread.Sleep(1); } //(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：                                                                           
                fileStream = new WICStream(wicFactory, bmpFilePath, NativeFileAccess.Read);
            }
            else { fileStream = new WICStream(wicFactory, bmpFilePath, NativeFileAccess.Read); }
            //(2)解析到BitmapDecoder中
            if (bitmapDecoder != null)
            {
                bitmapDecoder.Dispose();
                while (bitmapDecoder.IsDisposed == false) { Thread.Sleep(1); }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：                                                                              
                bitmapDecoder = new BitmapDecoder(imagingFactory, fileStream, DecodeOptions.CacheOnDemand);
            }
            else { bitmapDecoder = new BitmapDecoder(imagingFactory, fileStream, DecodeOptions.CacheOnDemand); }
            if (frame != null)
            {
                frame.Dispose();
                while (frame.IsDisposed == false) { Thread.Sleep(1); }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：
                frame = bitmapDecoder.GetFrame(0);//获取第1帧数据                                                    
            }
            else { frame = bitmapDecoder.GetFrame(0); }//获取第1帧数据
            //(3)设置格式转换器
            if (converter != null)
            {
                converter.Dispose();
                while (converter.IsDisposed == false) { Thread.Sleep(1); }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：       
                                                                          /*FormatConverter*/
                converter = new FormatConverter(imagingFactory);
            }
            else { converter = new FormatConverter(imagingFactory); }

            converter.Initialize(frame, SharpDX.WIC.PixelFormat.Format32bppPBGRA/*FormatBlackWhite*//*Format32bppPBGRA*//*Format32bppRGBA*//*Format32bppPRGBA*/);//设置格式转换器为单色BMP
            //(4)生成待显示的图像
            if (bitmapCollection[index] != null)
            {
                bitmapCollection[index].Dispose();
                while (bitmapCollection[index].IsDisposed == false) { Thread.Sleep(1); }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：
                bitmapCollection[index] = Bitmap1.FromWicBitmap(deviceContext, converter);
            }
            else { bitmapCollection[index] = Bitmap1.FromWicBitmap(deviceContext, converter); }
            ////(5)显示图片到二维GUI系统上
            //deviceContext.DrawBitmap(bitmap/*playerBitmap*/, 1.0f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear);
        }

        public void LoadingFromBMPFile3(int index)
        {
            bitmap=bitmapCollection[index];
        }
        public void DisposeBMPFile()//20210327新增：
        {
            //(4)生成待显示的图像
            if (bitmap != null)
            {
                bitmap.Dispose();
                while (bitmap.IsDisposed == false) { Thread.Sleep(1); }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：
            }
        }
        public void LoadingFromBMPFile(string bmpFilePath)
        {
            //(1)读取校准图bmp文件
            if (imagingFactory != null)
            {
                imagingFactory.Dispose();
                while (imagingFactory.IsDisposed == false) { Thread.Sleep(1); }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：
                imagingFactory = new ImagingFactory();
            }
            else
            {
                imagingFactory = new ImagingFactory();
            }

            if (fileStream != null)
            {
                fileStream.Dispose();
                while (fileStream.IsDisposed == false){ Thread.Sleep(1); } //(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：                                                                           
                fileStream = new WICStream(wicFactory, bmpFilePath,/* NativeFileAccess.Read*/NativeFileAccess.ReadWrite);
            }
            else
            {
                fileStream = new WICStream(wicFactory, bmpFilePath, /*NativeFileAccess.Read*/NativeFileAccess.ReadWrite);
            }
            //(2)解析到BitmapDecoder中
            if (bitmapDecoder != null)
            {
                bitmapDecoder.Dispose();
                while (bitmapDecoder.IsDisposed == false) { Thread.Sleep(1); }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：                                                                              
                bitmapDecoder = new BitmapDecoder(imagingFactory, fileStream, DecodeOptions.CacheOnDemand);
            }
            else { bitmapDecoder = new BitmapDecoder(imagingFactory, fileStream, DecodeOptions.CacheOnDemand); }
            if (frame != null)
            {
                frame.Dispose();
                while (frame.IsDisposed == false) { Thread.Sleep(1); }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：
                frame = bitmapDecoder.GetFrame(0);//获取第1帧数据                                                    
            }
            else
            {
                frame = bitmapDecoder.GetFrame(0);
            }//获取第1帧数据
            //(3)设置格式转换器
            if (converter != null)
            {
                converter.Dispose();
                while (converter.IsDisposed == false){ Thread.Sleep(1); }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：       
                converter = new FormatConverter(imagingFactory);
            }
            else
            {
                converter = new FormatConverter(imagingFactory);
            }
            
            converter.Initialize(frame, SharpDX.WIC.PixelFormat.Format32bppPBGRA/*FormatBlackWhite*//*Format32bppPBGRA*//*Format32bppRGBA*//*Format32bppPRGBA*/);//设置格式转换器为单色BMP
            //(4)生成待显示的图像
            if (bitmap != null)
            {
                bitmap.Dispose();
                while (bitmap.IsDisposed == false) { Thread.Sleep(1); }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：
                bitmap = Bitmap1.FromWicBitmap(deviceContext, converter);
            }
            else
            {
                bitmap = Bitmap1.FromWicBitmap(deviceContext, converter);
            }
            ////(5)显示图片到二维GUI系统上
            //deviceContext.DrawBitmap(bitmap/*playerBitmap*/, 1.0f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear);



            //20210327新增：使用完毕必须要善后，否则重新生成图片内存有问题
            //(1)读取校准图bmp文件
            if (imagingFactory != null)
            {
                imagingFactory.Dispose();
                while (imagingFactory.IsDisposed == false) { Thread.Sleep(1); }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：
            }
            if (fileStream != null)
            {
                fileStream.Dispose();
                while (fileStream.IsDisposed == false) { Thread.Sleep(1); } //(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：                                                                           
            }
            //(2)解析到BitmapDecoder中
            if (bitmapDecoder != null)
            {
                bitmapDecoder.Dispose();
                while (bitmapDecoder.IsDisposed == false) { Thread.Sleep(1); }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：                                                                              
            }
            if (frame != null)
            {
                frame.Dispose();
                while (frame.IsDisposed == false) { Thread.Sleep(1); }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：                                               
            }
            //(3)设置格式转换器
            if (converter != null)
            {
                converter.Dispose();
                while (converter.IsDisposed == false) { Thread.Sleep(1); }//(2)等待堆区释放，重新分配堆区，刷新显示//20210310新增修改：       
            }
        }


        float BMPscale = /*2.85f*/2.83f;
        //float heightwidthfactor = 1;//20210322取消：
        private void RenderingFromBMPFile()
        {
            if (bitmap != null)
            {
                //(5)显示图片到二维GUI系统上
                //heightwidthfactor = bitmap.Size.Width / bitmap.Size.Height;
                deviceContext.DrawBitmap(bitmap/*playerBitmap*/, new SharpDX.RectangleF(-330/*-420*/ * BMPscale * 0.5f, -330/*-350 */* BMPscale * 0.5f, 330/*420*/ * BMPscale, 330/*350*/ * BMPscale),
                    1f/*1.0f*/, SharpDX.Direct2D1.BitmapInterpolationMode.Linear);
            }
            else { }
        }

        public int k_dYJetOff = 0;//20210311修正：Y向的位置起始偏差。
        /// <summary>
        /// 20200609：传输数据测试;传输BMP格式，载入1层的BMP数据//20200409批注：内存中的bmp文件的存储方式是从上到下，从左到右；BMP文件的存储方式是从下到上，从左到右；           
        /// </summary>
        private int WriteImgLayerData(System.Drawing.Bitmap clone, int index, int subindex, int RePrintTimes,bool ReverseColor/*,int PrtDirFlag, bool SpreadPowerFlagDir*/)//201030修改：//必须放在1个独立的线程中//文件的本质就是保存在HD的字节流
        {        
            //(贰)校验传输的数据是否准确：20200409新增
            //(贰)校验传输的数据是否准确：20200409新增
            if (clone.PixelFormat != System.Drawing.Imaging.PixelFormat.Format1bppIndexed)//20230202新建:中间数据为1bpp数据，后续进一步处理为所需的2bpp或者3bpp数据
            {
                MessageBox.Show("目前不支持非单点图像的打印");
                return -1;//退出程序//20200411批注：本部分有待验证是否合理，理论上是不太存在问题的
            }
            //(叁)图像取反处理：20200409新增
            //(贰)图像取反处理：20200409新增
            // （1）processedBit//执行必要的位操作
            // （1）processedBit// Lock the bitmap's bits.  map.LockBits();//锁定到内存
            System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, clone.Width, clone.Height);//像素宽度以及像素高度
            System.Drawing.Imaging.BitmapData bmpData = clone.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, clone.PixelFormat);
            // （2）Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;//像素地址的第一行

            // （3）Declare an array to hold the bytes of the bitmap.
            int bytes = Math.Abs(bmpData.Stride) * clone.Height;//the size of BitmapData//20200423新增批注：Height是像素的大小，不是字节的大小
            byte[] rgbValues = new byte[bytes];
            // （4）Copy the RGB values into the array.
            Marshal.Copy(ptr, rgbValues, 0, bytes);/*System.Runtime.InteropServices.*/
#if true//20200610测试：本部分不必须：执行反色处理：20200703批注：//20210324新建：对于打印CAD数据，需要执行反色处理；对于打印校准图，不需要执行反色处理
            // （5）Set every third value to the opposite value
            if (ReverseColor == true)//20210324修改:对于CAD数据，需要执行反色处理；对于校准图数据，不需要执行反色处理
            {
                for (int counter = 0; counter < bytes; counter++)
                    rgbValues[counter] = (byte)~(rgbValues[counter]);
            }
            else { }
#endif
#region//20230202新建：根据1bpp,2bpp,3bpp++以及GrayScale来重新编码为最新需要下发的数据
            int bpp = gc_RysysParam.PixelGrayBits/*2*/;//打印数据格式
            int GrayScale = gc_RysysParam.PixelGrayValue/*2*/;//打印灰阶

            int BytePerLineForRgb1bppValues = (clone.Width * 1 + 31) / 32 * 4;//4byte对齐修正版本
            int BytePerLineForRgb2bppValues = (clone.Width * 2 + 31) / 32 * 4;//4byte对齐修正版本
            int BytePerLineForRgb3bppValues = (clone.Width * 3 + 31) / 32 * 4;//4byte对齐修正版本
            byte[] Rgb2bppValues = new byte[BytePerLineForRgb2bppValues * clone.Height];//new byte[2 * bytes];//2bpp打印数据//需要考虑4byte对齐:2bpp打印数据//需要考虑4byte对齐:
            byte[] Rgb3bppValues = new byte[BytePerLineForRgb3bppValues * clone.Height];//new byte[3 * bytes];//3bpp打印数据
            BitArray Rgb1bppBits = new BitArray(rgbValues);
            BitArray Rgb2bppBits = new BitArray(BytePerLineForRgb2bppValues * 8 * clone.Height);//new BitArray(Rgb2bppValues);
            BitArray Rgb3bppBits = new BitArray(BytePerLineForRgb3bppValues * 8 * clone.Height);//new BitArray(Rgb3bppValues);

            if (bpp == 1)//1bpp模式
            {
                //不需要执行任何操作
                //for (int i = 0; i < Rgb1bppBits.Length; i++)
                //{ 
                //}
             }
            else if (bpp == 2) //2bpp模式下的3个灰度等级（灰阶）
            {
                for (int i = 0; i < clone.Height; i++) //遍历所有1bpp的所有行
                {
                    for (int j = 0; j < clone.Width; j++) //遍历1bpp数据的所有列像素
                    {
                        if (Rgb1bppBits[i * BytePerLineForRgb1bppValues * 8 + j] == false)//判断指定行-指定列的1bpp数据的像素值
                        {
                            Rgb2bppBits[i * BytePerLineForRgb2bppValues * 8 + 2 * j + 1] = false; Rgb2bppBits[2 * j] = false;
                        }
                        else
                        {
                            switch (GrayScale)
                            {
                                case 1:
                                    Rgb2bppBits[i * BytePerLineForRgb2bppValues * 8 + 2 * j + 1] = false; Rgb2bppBits[i * BytePerLineForRgb2bppValues * 8 + 2 * j] = true;
                                    break;
                                case 2:
                                    Rgb2bppBits[i * BytePerLineForRgb2bppValues * 8 + 2 * j + 1] = true; Rgb2bppBits[i * BytePerLineForRgb2bppValues * 8 + 2 * j] = false;
                                    break;
                                case 3:
                                    Rgb2bppBits[i * BytePerLineForRgb2bppValues * 8 + 2 * j + 1] = true; Rgb2bppBits[i * BytePerLineForRgb2bppValues * 8 + 2 * j] = true;
                                    break;
                            }
                        }
                    }
                }                      
            }
            else if (bpp == 3) //3bpp模式下的7个灰度等级（灰阶）
            {
                for (int i = 0; i < clone.Height; i++) //遍历所有1bpp的所有行
                {
                    for (int j = 0; j < clone.Width; j++) //遍历1bpp数据的所有列像素
                    {
                        if (Rgb1bppBits[i * BytePerLineForRgb1bppValues * 8 + j] == false)//判断指定行-指定列的1bpp数据的像素值
                        {
                            Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j + 2] = false; Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j + 1] = false; Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j] = false;
                        }
                        else
                        {
                            switch (GrayScale)
                            {
                                case 1:
                                    Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j + 2] = false; Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j + 1] = false; Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j] = true;
                                    break;
                                case 2:
                                    Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j + 2] = false; Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j + 1] = true; Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j] = false;
                                    break;
                                case 3:
                                    Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j + 2] = false; Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j + 1] = true; Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j] = true;
                                    break;
                                case 4:
                                    Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j + 2] = true; Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j + 1] = false; Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j] = false;
                                    break;
                                case 5:
                                    Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j + 2] = true; Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j + 1] = false; Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j] = true;
                                    break;
                                case 6:
                                    Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j + 2] = true; Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j + 1] = true; Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j] = false;
                                    break;
                                case 7:
                                    Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j + 2] = true; Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j + 1] = true; Rgb3bppBits[i * BytePerLineForRgb3bppValues * 8 + 3 * j] = true;
                                    break;
                            }
                        }
                    }
                }
            }
            else { }
#endregion

            // （6）Copy the RGB values back to the bitmap
            /***********************20200423调试新增：************************/
            /***********************20200423调试新增：************************/
            IntPtr ImgPtr = new IntPtr();
            if (bpp == 1)
            {
                int size = Marshal.SizeOf(rgbValues[0]) * rgbValues.Length;
                ImgPtr = Marshal.AllocHGlobal(size);
                Marshal.Copy(rgbValues, 0, ImgPtr, rgbValues.Length);//复制到非托管区内存
            }
            else if (bpp == 2)
            {
                int size = Marshal.SizeOf(Rgb2bppValues[0]) * Rgb2bppValues.Length;
                ImgPtr = Marshal.AllocHGlobal(size);
                Rgb2bppBits.CopyTo(Rgb2bppValues, 0);//20230203新建批注：此处不存在BUG //Rgb2bppValues = ConvertToByteArray(Rgb2bppBits);
                Marshal.Copy(Rgb2bppValues, 0, ImgPtr, Rgb2bppValues.Length);//复制到非托管区内存
            }
            else if (bpp == 3)
            {
                int size = Marshal.SizeOf(Rgb3bppValues[0]) * Rgb3bppValues.Length;
                ImgPtr = Marshal.AllocHGlobal(size);
                Rgb3bppBits.CopyTo(Rgb3bppValues, 0);//20230203新建批注：此处不存在BUG //Rgb3bppValues = ConvertToByteArray(Rgb3bppBits);
                Marshal.Copy(Rgb3bppValues, 0, ImgPtr, Rgb3bppValues.Length);//复制到非托管区内存
            }

            /***********************20200423调试新增：************************/
            /***************************20200423调试新增：*************************/
            IntPtr[] NewImgPtr = new IntPtr[1/*3*/];//存放3种颜色的数组//20200423新增：//20230203修改：1种颜色
            NewImgPtr[0] = ImgPtr; ////NewImgPtr[1] = ImgPtr;//20200428新增//20230203修改：非必要 ////NewImgPtr[2] = ImgPtr;//20200428新增//20230203修改：非必要
            int size3 = Marshal.SizeOf(NewImgPtr[0]) * NewImgPtr.Length;
            IntPtr p_NewImgPtr = Marshal.AllocHGlobal(size3);
            Marshal.Copy(NewImgPtr, 0, p_NewImgPtr, NewImgPtr.Length);//复制到非托管区内存
            /***************************20200423调试新增：*************************/

            //（肆） 保存附带的所有必要的BMP数据
            //（肆） 处理图层信息
            royal.royal.g_prtimg_layer.nXEncOff = (int)(0/*gc_RysysParam.m_dXJetOff*200*/);//5um的精度//图像的XDPI，本质必须与光栅的DPI保持协调//20230321修订：X方向打印启打位置修订//20230327修正：此处存在潜在的问题//图层的整体偏移，可正可负
            //royal.royal.g_prtimg_layer.nYJetOff =;//20210311修正：Y向的位置起始偏差。
            //royal.royal.g_prtimg_layer.nYJetOff = k_dYJetOff/*(int)(g_RYSYSParam.m_dYJetOff * 600)*/;//20210311修正：Y向的位置起始偏差。
            royal.royal.g_prtimg_layer.nXDPI = XDpi/*635*//*XDpi*//*635*//*1270*2*//*635*/;//图像的XDPI，本质必须与光栅的DPI保持协调//20200802批注：修改原有的X向分辨率，本来应该是635DPI，提升到635*2DPI//20210324修改：打印校准图应该为635DPI
            royal.royal.g_prtimg_layer.nYDPI = 600;//图像的XDPI，本值必须与喷头的DPI保持一致
            if (bpp == 1)
            {
                royal.royal.g_prtimg_layer.nBytesPerLine = BytePerLineForRgb1bppValues; //bmpData.Stride * bpp;///*bmpData每行的数据字节数*/
            }
            else if (bpp == 2) 
            {
                royal.royal.g_prtimg_layer.nBytesPerLine = BytePerLineForRgb2bppValues;
            }
            else if (bpp == 3)
            {
                royal.royal.g_prtimg_layer.nBytesPerLine = BytePerLineForRgb3bppValues;
            }
            else { }
            royal.royal.g_prtimg_layer.nWidth = clone.Width;//bmpDat9a的像素宽度
            royal.royal.g_prtimg_layer.nHeight = clone.Height;//bmpData的像素高度
            //int i = 1;//第1层的数据：20200409新增：具体实现的时候，会移植到为爱面
            royal.royal.g_prtimg_layer.nLayerIndex = index* RePrintTimes + subindex;//发送图层的序号//201030修改：增加重喷控制参数//开始层索引为1
            royal.royal.g_prtimg_layer.nColorCnts = 1;//颜色个数，打印图形颜色为单色
            //不同的层需要进行不同的设置：20200429新增批注：单层需要正向打印，双层需要方向打印
            royal.royal.g_prtimg_layer.nPrtFlag = 1;//双向打印 bit[0] 控制单双向打印
            royal.royal.g_prtimg_layer.nPrtDir = 0/*((index * RePrintTimes + subindex) % 2)*/ /*1*//*1*//*PrtDirFlag*/;//起始打印方向为增序：光栅计数增大的方向开始计数//201030修改：增加重喷控制参数   
            //royal.royal.g_prtimg_layer.nImgStartJetIndex = (int)(g_RYSYSParam.m_dYJetOff / 25.4 * 600);//20210311新增：Y向起打位置修订//20210330修改：

#if true//20200610测试：测试生成的图片是否正确//20201118新增：方便调试
            //生成单比特位图测试
            if (bpp == 1)
            {
                System.Drawing.Bitmap out1bppBMP = new System.Drawing.Bitmap(
                    clone.Width * bpp, clone.Height, BytePerLineForRgb1bppValues, System.Drawing.Imaging.PixelFormat.Format1bppIndexed, ImgPtr/* + 54*/);//20200609新增：直接使用非托管内存，200ms执行时间
                out1bppBMP.Save("output1bpp.bmp", ImageFormat.Bmp);//保存到BMPFile
            }
            else if (bpp == 2)
            {
                System.Drawing.Bitmap out1bppBMP = new System.Drawing.Bitmap(
                    clone.Width * bpp, clone.Height, BytePerLineForRgb2bppValues, System.Drawing.Imaging.PixelFormat.Format1bppIndexed, ImgPtr/* + 54*/);//20200609新增：直接使用非托管内存，200ms执行时间
                out1bppBMP.Save("output1bpp.bmp", ImageFormat.Bmp);//保存到BMPFile
            }
            else if (bpp == 3)
            {
                System.Drawing.Bitmap out1bppBMP = new System.Drawing.Bitmap(
                    clone.Width * bpp, clone.Height, BytePerLineForRgb3bppValues, System.Drawing.Imaging.PixelFormat.Format1bppIndexed, ImgPtr/* + 54*/);//20200609新增：直接使用非托管内存，200ms执行时间
                out1bppBMP.Save("output1bpp.bmp", ImageFormat.Bmp);//保存到BMPFile
            }
            else { }
#endif

            //（伍） 完成数据的传输
            //（伍） 完成数据的传输
            int nRet = -1;//默认的数据为-1；//70ms:取反处理
#if true//测试数据传输：20200613批注：
            string msg = null;

            do
            {
                if (bpp == 1)
                {
                    nRet = royal.royal.IDP_WriteImgLayerData(ref royal.royal.g_prtimg_layer, p_NewImgPtr/*ImgPtr*/ /*ptr*/, BytePerLineForRgb1bppValues * clone.Height/*bytes * bpp*/);
                }
                else if (bpp == 2) 
                {
                    nRet = royal.royal.IDP_WriteImgLayerData(ref royal.royal.g_prtimg_layer, p_NewImgPtr/*ImgPtr*/ /*ptr*/, BytePerLineForRgb2bppValues * clone.Height/* bytes * bpp*/);
                }
                else if (bpp == 3) 
                {
                    nRet = royal.royal.IDP_WriteImgLayerData(ref royal.royal.g_prtimg_layer, p_NewImgPtr/*ImgPtr*/ /*ptr*/, BytePerLineForRgb3bppValues * clone.Height /*bytes * bpp*/);
                }

                if (nRet > 0)//返回值是33，计算出来的PASS总数；只要在PCS里面进行修改，即可然返回的值发生变化
                {
                    //break;
                    nRet = 2;//总之nRet大于0即可，调出当前循环
                }
                else//20200409批注：分析错误号，做出相应处理：提示或者其他处理均可以
                {
                    switch (nRet)
                    {
                        case -110000://没有按照顺序，增加索引号
                            /*string*/ msg = "作业写入成功：IDP_WriteImgLayerData：{-110000: 作业启动失败：指定图层打印的PASS总数}" + $"bpp:{{{bpp}bit}}灰阶:{{{GrayScale}阶}}" +
                                $"nLayerIndex{{{ royal.royal.g_prtimg_layer.nLayerIndex}}}LayerIndex{{{index}}}SubIndex{{{subindex}}}RePrintTimes{{{RePrintTimes}}}" +
                                $"nBytesPerLine{{{royal.royal.g_prtimg_layer.nBytesPerLine}}}" +
                                $"nWidth{{{royal.royal.g_prtimg_layer.nWidth}}}nHeight{{{royal.royal.g_prtimg_layer.nHeight}}}" +

                                $"nXEncOff{{{ royal.royal.g_prtimg_layer.nXEncOff}}}nYJetOff{{{ royal.royal.g_prtimg_layer.nYJetOff}}}" +
                                $"nImgStartJetIndex{{{ royal.royal.g_prtimg_layer.nImgStartJetIndex}}}" +
                                $"nXDPI{{{ royal.royal.g_prtimg_layer.nXDPI}}}nYDPI{{{ royal.royal.g_prtimg_layer.nYDPI}}}" +
                                $"nPrtDir{{{ royal.royal.g_prtimg_layer.nPrtDir}}} nPrtFlag{{{ royal.royal.g_prtimg_layer.nPrtFlag}}}" +
                                $"nColorCnts{{{ royal.royal.g_prtimg_layer.nColorCnts}}}" +
                                $"nReserved{{{ royal.royal.g_prtimg_layer.nReserved}}}";
                            Log4Net.Info(msg);

                            MessageBox.Show("作业启动失败：指定图层打印的PASS总数");
                            break;
                        case -110001:
                            /*string*/ msg = "作业写入成功：IDP_WriteImgLayerData：{-110001: 作业启动失败：PC内存不足}" + $"bpp:{{{bpp}bit}}灰阶:{{{GrayScale}阶}}" +
                                $"nLayerIndex{{{ royal.royal.g_prtimg_layer.nLayerIndex}}}LayerIndex{{{index}}}SubIndex{{{subindex}}}RePrintTimes{{{RePrintTimes}}}" +
                                $"nBytesPerLine{{{royal.royal.g_prtimg_layer.nBytesPerLine}}}" +
                                $"nWidth{{{royal.royal.g_prtimg_layer.nWidth}}}nHeight{{{royal.royal.g_prtimg_layer.nHeight}}}" +

                                $"nXEncOff{{{ royal.royal.g_prtimg_layer.nXEncOff}}}nYJetOff{{{ royal.royal.g_prtimg_layer.nYJetOff}}}" +
                                $"nImgStartJetIndex{{{ royal.royal.g_prtimg_layer.nImgStartJetIndex}}}" +
                                $"nXDPI{{{ royal.royal.g_prtimg_layer.nXDPI}}}nYDPI{{{ royal.royal.g_prtimg_layer.nYDPI}}}" +
                                $"nPrtDir{{{ royal.royal.g_prtimg_layer.nPrtDir}}} nPrtFlag{{{ royal.royal.g_prtimg_layer.nPrtFlag}}}" +
                                $"nColorCnts{{{ royal.royal.g_prtimg_layer.nColorCnts}}}" +
                                $"nReserved{{{ royal.royal.g_prtimg_layer.nReserved}}}";
                            Log4Net.Info(msg);

                            MessageBox.Show("作业启动失败：PC内存不足");
                            break;
                        case -110002:
                            /*string*/ msg = "作业写入成功：IDP_WriteImgLayerData：{-110002: 作业启动失败：PASS计算小于0}" + $"bpp:{{{bpp}bit}}灰阶:{{{GrayScale}阶}}" +
                                $"nLayerIndex{{{ royal.royal.g_prtimg_layer.nLayerIndex}}}LayerIndex{{{index}}}SubIndex{{{subindex}}}RePrintTimes{{{RePrintTimes}}}" +
                                $"nBytesPerLine{{{royal.royal.g_prtimg_layer.nBytesPerLine}}}" +
                                $"nWidth{{{royal.royal.g_prtimg_layer.nWidth}}}nHeight{{{royal.royal.g_prtimg_layer.nHeight}}}" +

                                $"nXEncOff{{{ royal.royal.g_prtimg_layer.nXEncOff}}}nYJetOff{{{ royal.royal.g_prtimg_layer.nYJetOff}}}" +
                                $"nImgStartJetIndex{{{ royal.royal.g_prtimg_layer.nImgStartJetIndex}}}" +
                                $"nXDPI{{{ royal.royal.g_prtimg_layer.nXDPI}}}nYDPI{{{ royal.royal.g_prtimg_layer.nYDPI}}}" +
                                $"nPrtDir{{{ royal.royal.g_prtimg_layer.nPrtDir}}} nPrtFlag{{{ royal.royal.g_prtimg_layer.nPrtFlag}}}" +
                                $"nColorCnts{{{ royal.royal.g_prtimg_layer.nColorCnts}}}" +
                                $"nReserved{{{ royal.royal.g_prtimg_layer.nReserved}}}";
                            Log4Net.Info(msg);
                            MessageBox.Show("作业启动失败：PASS计算小于0");
                            break;
                    }
                    break;
                }
            } while (nRet <= 0);
            /*string*/ msg = "作业写入成功：IDP_WriteImgLayerData：" + $"bpp:{{{bpp}bit}}灰阶:{{{GrayScale}阶}}"+
                $"nLayerIndex{{{ royal.royal.g_prtimg_layer.nLayerIndex}}}LayerIndex{{{index}}}SubIndex{{{subindex}}}RePrintTimes{{{RePrintTimes}}}"+
                $"nBytesPerLine{{{royal.royal.g_prtimg_layer.nBytesPerLine}}}" +
                $"nWidth{{{royal.royal.g_prtimg_layer.nWidth}}}nHeight{{{royal.royal.g_prtimg_layer.nHeight}}}" +
                
                $"nXEncOff{{{ royal.royal.g_prtimg_layer.nXEncOff}}}nYJetOff{{{ royal.royal.g_prtimg_layer.nYJetOff}}}" +
                $"nImgStartJetIndex{{{ royal.royal.g_prtimg_layer.nImgStartJetIndex}}}" +
                $"nXDPI{{{ royal.royal.g_prtimg_layer.nXDPI}}}nYDPI{{{ royal.royal.g_prtimg_layer.nYDPI}}}" +
                $"nPrtDir{{{ royal.royal.g_prtimg_layer.nPrtDir}}} nPrtFlag{{{ royal.royal.g_prtimg_layer.nPrtFlag}}}" +
                $"nColorCnts{{{ royal.royal.g_prtimg_layer.nColorCnts}}}" +
                $"nReserved{{{ royal.royal.g_prtimg_layer.nReserved}}}";
            Log4Net.Info(msg);

#endif
            /***********************20200423调试新增：************************/
            //int size2 = Marshal.SizeOf(rgbValues[0]) * rgbValues.Length;
            //IntPtr ImgPtr = Marshal.AllocHGlobal(size2);
            //Marshal.Copy(rgbValues, 0, ImgPtr, rgbValues.Length);//复制到非托管区内存

            /***************************20200423调试新增：*************************/
            Marshal.FreeHGlobal(p_NewImgPtr);//释放内存/***************************20200423调试新增：*************************/
            Marshal.FreeHGlobal(ImgPtr);//释放内存/***********************20200423调试新增：************************/           

            //（陆） 释放对应的数据
            //（陆） 释放对应的数据
            // （7）Unlock the bits.
            clone.UnlockBits(bmpData);
            //MessageBox.Show("生成完成");
            //SaveBMPBtn.BackColor = System.Drawing.Color.LightCyan;

            return nRet;//返回核心代码——IDP_WriteImgLayerData——的执行结果
            //反色测试：20200409新增
            //processedBitmap.Save(@"C:\Users\SummerGhost\Documents\Visual Studio 2017\Projects\LaserAdd_3DP_Software\1-3DP主控界面(人机交互模块)\bin\x64\Debug\JOB输出文件\输出-反色-2.bmp", System.Drawing.Imaging.ImageFormat.Bmp);//————保存到BMP文件:20200408修改
            //processedBitmap.Dispose();//及时释放掉clone：20200408新增

            //（柒） 用不着
            //（柒） 用不着
            // （8）Another edit way: Draw the modified image.//modified the BMP file with graphics in VC
            //e.Graphics.DrawImage(bmp, 0, 150);
        }
        private /*static*/ byte[] ConvertToByteArray(BitArray bitArray)//20230203新建：BitArray转换为Byte[]
        {
            // pack (in this case, using the first bool as the lsb - if you want
            // the first bool as the msb, reverse things ;-p)
            int bytes = (bitArray.Length + 7) / 8;
            byte[] arr2 = new byte[bytes];
            int bitIndex = 0;
            int byteIndex = 0;

            for (int i = 0; i < bitArray.Length; i++)
            {
                if (bitArray[i])
                {
                    arr2[byteIndex] |= (byte)(1 << bitIndex);
                }

                bitIndex++;
                if (bitIndex == 8)
                {
                    bitIndex = 0;
                    byteIndex++;
                }
            }
            return arr2;
        }

        /// <summary>
        /// (3)释放渲染资源
        /// </summary>
        public void DisposeClosing()
        {
            DestructRenderToWic();//20200610析构数据处理及传送

            Thread.Sleep(500);//等待50ms.释放所有的文件
            Factory2D.Dispose();//20200523新建：
            strokeStyle2.Dispose();//20200523新建：    
            strokeStyle3.Dispose();//20201107新增：
            strokeStyle4.Dispose();//20201107新增：
            strokeStyle1.Dispose();
            dwFactory.Dispose();
            textFormat.Dispose();
            textFormat2.Dispose();
            textLayout.Dispose();
            textLayout2.Dispose();
            textLayout3.Dispose();
            textFormat3.Dispose();
            textFormat4.Dispose();
            BaseBrush.Dispose();
            RulerBackBrush.Dispose();//20200612新增
            RulerBackBrush2.Dispose();//20201109新增
            RulerLineBrush.Dispose();//20200612新增
            BaseLineBrush.Dispose();//20200612新增
            CoordinatLineBrush.Dispose();//20200612新增
            CornerCoverBrush.Dispose();//20200612新增

            LocationHoleBrush.Dispose();
            PartSolidBrush.Dispose();
            PartSolidBrush2.Dispose();
            solidBrush.Dispose();
            OutlineBrush.Dispose();
            solidBrush3.Dispose();
            solidBrushBackLable.Dispose();
            solidBrushTopLeftLable.Dispose();
            linearGradientBrush.Dispose();//绘制主界面背景渐变填充
            //if (linearGradientBrush2 != null)//绘制主界面背景渐变填充
            //{  linearGradientBrush2.Dispose(); }
            //注意，结构体是没有dispose()方法的，意味着：结构体不在非托管区的堆区分配内存
            gradientStops.Dispose();//20210309新增：
    }


        //20200524新建：
        private void DrawPositionHole(float xCenter, float yCenter, float radius, SolidColorBrush transparentBrush1, SolidColorBrush transparentBrush)
        {
            Ellipse ellipse = new Ellipse(new RawVector2(-xCenter * mm2Dip, -yCenter * mm2Dip), radius * mm2Dip, radius * mm2Dip);//中心,X,Y
                                                                                                                                  //hole 1:
            transparentBrush.Opacity = 0.85f;//设置透明程度
            deviceContext.FillEllipse(ellipse, transparentBrush);
            deviceContext.DrawEllipse(ellipse, transparentBrush1);
            //hole 2:
            ellipse = new Ellipse(new RawVector2(xCenter * mm2Dip, -yCenter * mm2Dip), radius * mm2Dip, radius * mm2Dip);//中心,X,Y
            deviceContext.FillEllipse(ellipse, transparentBrush);
            deviceContext.DrawEllipse(ellipse, transparentBrush1);
            //hole 3:
            ellipse = new Ellipse(new RawVector2(-xCenter * mm2Dip, yCenter * mm2Dip), radius * mm2Dip, radius * mm2Dip);//中心,X,Y
            deviceContext.FillEllipse(ellipse, transparentBrush);
            deviceContext.DrawEllipse(ellipse, transparentBrush1);
            //hole 4:
            ellipse = new Ellipse(new RawVector2(xCenter * mm2Dip, yCenter * mm2Dip), radius * mm2Dip, radius * mm2Dip);//中心,X,Y
            deviceContext.FillEllipse(ellipse, transparentBrush);
            deviceContext.DrawEllipse(ellipse, transparentBrush1);
            transparentBrush.Opacity = 1f;//设置透明程度
        }

        //20200522新增：
        public bool initFLag = false;
        float mainRuler = 5;//主比例尺尺寸1
        float ruler = 0;
        float m_zoomScale = 1.2f/*1.2f*/;//初始化缩放比例=4//20200524修改为2
        float m_zoomScaleBetween = 1;
        float increaseRate = 0.5f;
        public bool RePaintFlag = false;//20200524新增：避免重绘事件和滚轮缩放事件的冲突
        /// <summary>
        ///  (2)重绘事件绘制：
        /// </summary>
        /// <param name="renderControl1"></param>
        public void RepaintControl(bool CLIImportFlag,int CLIlayerIndex, System.Drawing.RectangleF renderControl1)
        {
            SharpDX.RectangleF ControlRectangle = new SharpDX.RectangleF(
                renderControl1.Top,
                renderControl1.Left,
                renderControl1.Width,
                renderControl1.Height);
            RePaintFlag = true;
            DrawDevice(CLIImportFlag, CLIlayerIndex, ControlRectangle);
        }
        
        public void RepaintDefaultControl(bool CLIImportFlag, int CLIlayerIndex, System.Drawing.RectangleF renderControl1)//双击重绘
        {
            SharpDX.RectangleF ControlRectangle = new SharpDX.RectangleF(
                renderControl1.Top,
                renderControl1.Left,
                renderControl1.Width,
                renderControl1.Height);
            RePaintFlag = true;
#if true//双击缩放会初始坐标
            //mainRuler = 5;//主比例尺尺寸1
            //m_zoomScale = 1.2f/*1.2f*/;//初始化缩放比例=4//20200524修改为2
            //m_zoomScaleBetween = 1;
            //increaseRate = 0.5f;


            viewportbase.X = ControlRectangle.Width / 2 + 3;
            viewportbase.Y = ControlRectangle.Height / 2;
            initFLag = true;

            m_zoomScaleBetween = 0.5f * 120f/*e.Delta*/ / 120f/*zDelta/120f*/;//20200524批注：缩放间距
            m_zoomScale = /*0.9f*/0.5f;
            mainRuler = 50;
#endif
            DrawDevice(CLIImportFlag, CLIlayerIndex, ControlRectangle);
        }

        public int k_nPrepareDeleteFlag = 1;//201106新增：默认为1，等待开启选中
        public System.Drawing.Point[] k_pSelectArea = new System.Drawing.Point[2];//201106新增：//选中操作区域实际上是2点矩形//更新2点矩形坐标即可

        /// <summary>
        /// (2-2)控件绘制：
        /// </summary>
        /// <param name="renderControl1"></param>
        public void PaintControl(bool CLIImportFlag,int CLIlayerIndex, System.Drawing.RectangleF renderControl1)
        {
            SharpDX.RectangleF ControlRectangle = new SharpDX.RectangleF(
                renderControl1.Top,
                renderControl1.Left,
                renderControl1.Width,
                renderControl1.Height);
            DrawDevice(CLIImportFlag, CLIlayerIndex, ControlRectangle);
        }


        float mm2Dip = 1 / 25.4f * 96 * 0.75f;
        /// <summary>
        /// (3)控件尺寸变换事件处理：精华
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ResizeControl(System.Drawing.Rectangle renderControl1)
        {
            //InitiateDevice();//非常关键：中间可能存在奔溃//显卡内存不做的BUG
            d3DDevice.ImmediateContext.ClearState();/*dxgiDevice*//*d2DDevice*//*device*///非常关键
            //d3DDevice.ImmediateContext.Flush();//不必要每次都调用这个，影响GPU的性能
            if (deviceContext != null)
            {
                deviceContext.Dispose();
            }
            backBuffer2.Dispose();
            if (targetBitmap != null)
            {
                targetBitmap.Dispose();/*renderView*/
            }
            if (surface != null)
            {
                surface.Dispose();/*backBuffer*/
            }

            SwapChain2.ResizeBuffers(1, renderControl1.Width * 2, renderControl1.Height * 2, Format.R8G8B8A8_UNorm, SwapChainFlags.AllowModeSwitch);//非常关键
            backBuffer2 = Texture2D.FromSwapChain<Texture2D>(SwapChain2, 0);
            this.surface/*backBuffer*/ = backBuffer2.QueryInterface<Surface>();//直接使用surface很垃圾，会产生莫名其妙错误：surface = Surface.FromSwapChain(SwapChain2, 0);//backBuffer = Texture2D.FromSwapChain<Texture2D>(SwapChain2, 0);//surface = backBuffer.QueryInterface<Surface>();

            this.deviceContext = new DeviceContext(d2DDevice, DeviceContextOptions.EnableMultithreadedOptimizations/*None*/);//开启多线程优化：EnableMultithreadedOptimizations
            Size2F size2F = new Size2F/*(96*4, 96*4)*/(96 * 2f, 96 * 2f);//设备无关像素单位设置：≈=物理单位设置
            deviceContext.DotsPerInch = size2F;
            deviceContext.UnitMode = 0;
            targetBitmap = new Bitmap1(deviceContext, surface);//renderView = new RenderTargetView(device, backBuffer); 
            deviceContext.Target = targetBitmap;//d2dRenderTarget = new RenderTarget(d2dFactory, surface, new RenderTargetProperties(new PixelFormat(Format.Unknown, AlphaMode.Premultiplied)));
        }

        /// <summary>
        /// (4)滚轮滚动时间处理：精华
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void MouseWheelControl(MouseEventArgs e,PointF point)
        {
            CursorPointF = point;
            m_zoomScaleBetween = 0.5f * e.Delta / 120f/*zDelta/120f*/;//20200524批注：缩放间距
            m_zoomScale += 0.5f * e.Delta / 120f;
            if (m_zoomScale <= 0.5)
            {
                m_zoomScale = 0.5f;
                mainRuler = 50;
            }
            if (1 < m_zoomScale && m_zoomScale <= 1.5)
            {
                mainRuler = 50;
            }
            if (1.5 < m_zoomScale && m_zoomScale <= 3)
            {
                mainRuler = 25;
            }
            if (3 < m_zoomScale && m_zoomScale <= 4)
            {
                mainRuler = 20/*25*/;//20210321修改：
            }
            if (4 < m_zoomScale && m_zoomScale <= 12)
            {
                mainRuler = 10/*20*/;//increaseRate = 1.2f;//20210321修改：
            }
            if (12 < m_zoomScale && m_zoomScale <= 20)
            {
                mainRuler = 5/*10*/; //increaseRate = 1.5f;//20210321修改：
            }
            if (20 < m_zoomScale && m_zoomScale <= 35)
            {
                mainRuler = 1/*5*/; //increaseRate = 1.8f;//20210321修改：
            }
            if (35 < m_zoomScale && m_zoomScale <= 50)
            {
                mainRuler = 1;//increaseRate = 2.2f;//20210321修改：
            }
            if (50 < m_zoomScale)
            {
                m_zoomScale = 50;
            }
        }
#if false
        //(1)关闭事件：
        protected override void OnClosing(CancelEventArgs e)
        {
            DisposeClosing();
            base.OnClosing(e);
        }
#endif
#if false
        /// (2)控件重绘事件
        private void renderControl1_Paint(object sender, PaintEventArgs e)
        {
            RepaintControl(new RectangleF(renderControl1.Top, renderControl1.Left, renderControl1.Width, renderControl1.Height));
        }
#endif
#if false
        //(3)控件尺寸变换事件处理：精华
        private void renderControl1_SizeChanged_1(object sender, EventArgs e)
        {
            //20200524新增：
            MouseWheel -= new MouseEventHandler(Form1_MouseWheel);//添加鼠标滚轮事件//迁移到InitializeComponent中间//20200524新增：消除BUG至关重要
            ResizeControl(new Rectangle(renderControl1.Top, renderControl1.Left, renderControl1.Width, renderControl1.Height));
            MouseWheel += new MouseEventHandler(Form1_MouseWheel);//添加鼠标滚轮事件//迁移到InitializeComponent中间//20200524新增：消除BUG至关重要
            //InitiateDeviceResize();
            PaintControl(new RectangleF(renderControl1.Top, renderControl1.Left, renderControl1.Width, renderControl1.Height));
            //d3DDevice.ImmediateContext.Flush();//不必要每次都调用这个，影响GPU的性能
        }
#endif
#if false
        private void Form1_MouseWheel(object sender, MouseEventArgs e)// (4)滚轮滚动时间处理：精华
        {
            if (this.renderControl1.ClientRectangle.Contains(panel3.PointToClient(Cursor.Position)))
            {
                PointF point = renderControl1.PointToClient(Cursor.Position);

                MouseWheelControl(point, new RectangleF(renderControl1.Top, renderControl1.Left, renderControl1.Width, renderControl1.Height));

                PaintControl(new RectangleF(renderControl1.Top, renderControl1.Left, renderControl1.Width, renderControl1.Height));
            }
        }
#endif
    }
}
