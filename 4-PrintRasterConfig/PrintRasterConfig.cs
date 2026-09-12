using System;

namespace LaserAdd.PrintRaster
{
    /// <summary>
    /// 全解决方案唯一幅面与切片光栅参数。修改此处并重新生成即可同步 RIP、Meteor 整层光栅与排版边界。
    /// </summary>
    public static class PrintRasterConfig
    {
        public const int PlateWidthMm = 100;
        // 有效幅面暂沿用小型机 100 × 60 mm，独立于墨车 118–412 mm 机械行程。
        public const float PlateHeightMm = 60;
        /// <summary>与 AutoPrintThread5 passStartBaseY 一致（mm，平台坐标，0=下沿）。</summary>
        public const double MeteorPassYInstallCompensationMm = 1.5;
        public const double MeteorPassStartBaseYMm = 0;
        /// <summary>与 InkCarPassPitchYMm 一致：64.96×2 mm。</summary>
        public const double MeteorPassPitchYMm = 0;
        public const int MeteorPassCountPerLayer = 1;
        /// <summary>RIP 整板与主程序 Meteor 单层光栅默认 DPI（与历史 RipPlateConfig.RipDpi 一致）。</summary>
        public const int SliceDpi = 400;

        public const string PhysicalHomeDoubleScanModeEnv = "METEOR_PHYSICAL_HOME_DOUBLE_SCAN";
        public const string PhysicalHomeDoubleScanShiftPixelsEnv = "METEOR_PHYSICAL_HOME_DOUBLE_SCAN_SHIFT_PX";
        public const int PhysicalHomeDoubleScanDefaultShiftPixels = 10;

        /// <summary>仅在 physical_home_fast 分支中允许启用同层两轮 3PASS。</summary>
        public static bool IsPhysicalHomeDoubleScanEnabled()
        {
            // 无 Y 轴，不允许旧环境变量重新启用两轮三 PASS。
            return false;
        }

        public static int GetPhysicalHomeDoubleScanShiftPixels()
        {
            try
            {
                string value = Environment.GetEnvironmentVariable(PhysicalHomeDoubleScanShiftPixelsEnv);
                int pixels;
                if (!string.IsNullOrWhiteSpace(value) && int.TryParse(value.Trim(), out pixels))
                    return Math.Max(1, Math.Min(256, pixels));
            }
            catch { }
            return PhysicalHomeDoubleScanDefaultShiftPixels;
        }

        public static double GetPhysicalHomeDoubleScanShiftMm(double dpi)
        {
            if (dpi <= 0.0)
                dpi = SliceDpi;
            return GetPhysicalHomeDoubleScanShiftPixels() * 25.4 / dpi;
        }

        public static int GetPhysicalHomeLayerScanStepCount()
        {
            return IsPhysicalHomeDoubleScanEnabled() ? 6 : MeteorPassCountPerLayer;
        }

        public static float PlateCenterOffsetXMm => PlateWidthMm * 0.5f;
        public static float PlateCenterOffsetYMm => PlateHeightMm * 0.5f;

        /// <summary>与 RIP CreateFinalJOB / BuildBmp 一致：<c>(int)(mm * dpi / 25.4) + 1</c>。</summary>
        public static int MmToPixelsPlusOne(double mm, int dpi)
        {
            return (int)(mm * dpi / 25.4) + 1;
        }

        public static int FullPlateBitmapWidthPixels => MmToPixelsPlusOne(PlateWidthMm, SliceDpi);
        public static int FullPlateBitmapHeightPixels => MmToPixelsPlusOne(PlateHeightMm, SliceDpi);

        /// <summary>
        /// 喷头在<strong>副扫描（走纸）</strong>方向上，单次成像块对应的<strong>喷嘴可寻址行数</strong>：
        /// 新农头约为 <b>1024 行 / 64.96mm</b>（与 Meteor 译码一包数据行对齐）；
        /// 老款喷头在 RIP 多 PASS（<c>CreatTwoPassFigure</c>）层内曾对应 <b>1280</b> 行、约五十多毫米幅宽的工艺。
        /// 更换喷头时请将本常量与喷头/METEOR 配置保持一致；多 PASS 拼图条高应优先使用本值而非按 DPI×mm 重算，
        /// 除非已确认 Raster DPI 等同于喷嘴寻址 DPI。
        /// </summary>
        public const int SwathBaseRowsPerPrintHead = 1024;
        /// <summary>1=单喷头条带 1024 行；2=双喷头条带 2048 行。与 Meteor/喷头 cfg 需一致。切回单喷改为 1 并重新生成。</summary>
        public const int PrintHeadCount = 1;
        /// <summary>Y 向 swath 条带高度（像素行）= <see cref="SwathBaseRowsPerPrintHead"/> * <see cref="PrintHeadCount"/>。</summary>
        public static int SwathStripHeightPixels => SwathBaseRowsPerPrintHead * PrintHeadCount;

        /// <summary>与现有 CLI 多边形缩放 <c>Convert.ToInt32(dpi / 25.4)</c> 一致。</summary>
        public static int PixelsPerMmTrunc(int dpi)
        {
            return (int)(dpi / 25.4);
        }
    }
}
