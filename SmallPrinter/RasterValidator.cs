using System;
using System.Collections.Generic;

namespace LaserAdd.SmallPrinter
{
    public static class RasterValidator
    {
        public static IList<string> Validate(SmallPrinterProfile profile, int widthPixels, int heightPixels)
        {
            if (profile == null) throw new ArgumentNullException("profile");

            var errors = new List<string>();
            if (widthPixels != profile.RasterWidthPixels)
                errors.Add(String.Format("光栅宽度为 {0} px，应为 {1} px（{3} mm @ {2} DPI）。",
                    widthPixels, profile.RasterWidthPixels, profile.SliceDpi, profile.PlateWidthMm));
            if (heightPixels != profile.RasterHeightPixels)
                errors.Add(String.Format("光栅高度为 {0} px，应为 {1} px（{3} mm @ {2} DPI）。",
                    heightPixels, profile.RasterHeightPixels, profile.SliceDpi, profile.PlateHeightMm));
            if (profile.PassesPerLayer != 1)
                errors.Add("该设备只能向 Meteor 发送单 PASS 光栅。");
            if (profile.PrintHeadCount != 1)
                errors.Add("该设备只能使用单喷头光栅。");
            if (profile.SwathHeightPixels < heightPixels)
                errors.Add(String.Format("单喷头条带高度 {0} px 小于整层高度 {1} px，不能单 PASS 覆盖。",
                    profile.SwathHeightPixels, heightPixels));
            return errors;
        }

        public static void ValidateOrThrow(SmallPrinterProfile profile, int widthPixels, int heightPixels)
        {
            IList<string> errors = Validate(profile, widthPixels, heightPixels);
            if (errors.Count > 0)
                throw new InvalidOperationException("切片光栅不适用于该小型设备：" + String.Join("；", errors));
        }
    }
}
