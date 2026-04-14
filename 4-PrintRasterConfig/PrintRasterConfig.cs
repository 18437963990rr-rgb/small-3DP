namespace LaserAdd.PrintRaster
{
    /// <summary>
    /// 全解决方案唯一幅面与切片光栅参数。修改此处并重新生成即可同步 RIP、Meteor 整层光栅与排版边界。
    /// </summary>
    public static class PrintRasterConfig
    {
        public const int PlateWidthMm = 465;
        public const int PlateHeightMm = 370;
        /// <summary>RIP 整板与主程序 Meteor 单层光栅默认 DPI（与历史 RipPlateConfig.RipDpi 一致）。</summary>
        public const int SliceDpi = 400;

        public static float PlateCenterOffsetXMm => PlateWidthMm * 0.5f;
        public static float PlateCenterOffsetYMm => PlateHeightMm * 0.5f;

        /// <summary>与 RIP CreateFinalJOB / BuildBmp 一致：<c>(int)(mm * dpi / 25.4) + 1</c>。</summary>
        public static int MmToPixelsPlusOne(double mm, int dpi)
        {
            return (int)(mm * dpi / 25.4) + 1;
        }

        public static int FullPlateBitmapWidthPixels => MmToPixelsPlusOne(PlateWidthMm, SliceDpi);
        public static int FullPlateBitmapHeightPixels => MmToPixelsPlusOne(PlateHeightMm, SliceDpi);

        /// <summary>与现有 CLI 多边形缩放 <c>Convert.ToInt32(dpi / 25.4)</c> 一致。</summary>
        public static int PixelsPerMmTrunc(int dpi)
        {
            return (int)(dpi / 25.4);
        }
    }
}
