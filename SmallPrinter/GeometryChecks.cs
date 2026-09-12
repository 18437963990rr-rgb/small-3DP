using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;

namespace LaserAdd.SmallPrinter
{
    public static class GeometryChecks
    {
        private sealed class Hardware : ISmallPrinterMotion, ISmallPrinterIo, IMeteorSinglePassPrinter
        {
            public readonly List<string> Calls = new List<string>();
            public CancellationTokenSource CancelOnFeed;
            public void EnsureAxisHomed(short axis) { Calls.Add("home:" + axis); }
            public void MoveTo(short axis, double position, double speed) { Calls.Add("move:" + axis + ":" + position); }
            public void WaitForIdle(short axis, TimeSpan timeout, CancellationToken token) { token.ThrowIfCancellationRequested(); }
            public void StopAll() { Calls.Add("stop"); }
            public void SetDigitalOutput(short channel, bool enabled, bool activeLow)
            {
                Calls.Add("do:" + channel + ":" + enabled);
                if (channel == 11 && enabled && CancelOnFeed != null) CancelOnFeed.Cancel();
            }
            public void SetAnalogOutputPercent(short channel, double value) { }
            public void DisableAllOutputs() { Calls.Add("outputs-off"); }
            public void PrepareScan(string path, int width, int height, bool towardsHighEnd, CancellationToken token) { Calls.Add(towardsHighEnd ? "print:out" : "print:back"); }
            public void CompleteScan(CancellationToken token) { token.ThrowIfCancellationRequested(); Calls.Add("scan-complete"); }
            public void Stop() { Calls.Add("meteor-stop"); }
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL: " + name);
        }

        public static string Run(string path)
        {
            var profile = SmallPrinterProfile.Load(path);
            Check(profile.GetAxis(AxisRole.InkCarX).MinimumPositionMm == 0 && profile.GetAxis(AxisRole.InkCarX).MaximumPositionMm == 370, "X mechanical bounds");
            Check(profile.GetAxis(AxisRole.InkCarX).HomePositionMm == 0, "X origin");
            Check(profile.InkCarMoisturizingPositionMm == 20 && profile.InkCarPrintMinimumMm == 118 && profile.InkCarPrintMaximumMm == 370, "moisturizing and print bounds are separate");
            Check(profile.GetAxis(AxisRole.PowderCar).MinimumPositionMm == 5 && profile.GetAxis(AxisRole.PowderCar).MaximumPositionMm == 255, "powder bounds");
            Check(profile.GetOutput(OutputRole.PowderFeedEnable).Channel == 11 && profile.GetOutput(OutputRole.PowderFeedEnable).ActiveLow, "EXO10 maps to API11 active low");
            Check(profile.HeadCleaning != null && profile.HeadCleaning.Enabled
                && profile.HeadCleaning.PressInkPositionMm == 155
                && profile.HeadCleaning.CleanPositionMm == 118
                && profile.HeadCleaning.PressInkDurationMs == 1500,
                "head-cleaning positions and duration");
            Check(profile.HeadCleaning.ScraperPulsesPerRevolution == 6400
                && profile.HeadCleaning.ScraperWipeAngleDegrees == 145
                && profile.GetOutput(OutputRole.PressInkEnable).Channel == 13
                && profile.GetOutput(OutputRole.PressInkEnable).ActiveLow,
                "scraper conversion and press-ink output");
            profile.GetOutput(OutputRole.PressInkEnable).Channel = -1;
            Check(profile.Validate().Any(error => error.Contains("PressInkEnable")), "unconfigured press-ink IO rejected");
            profile.GetOutput(OutputRole.PressInkEnable).Channel = 13;
            profile.GetOutput(OutputRole.PowderFeedEnable).Channel = -1;
            Check(profile.Validate().Any(error => error.Contains("PowderFeedEnable")), "unconfigured IO rejected");
            var hardware = new Hardware();
            var manual = new ManualMotionController(profile, hardware);
            foreach (double target in new[] {-0.001, 370.001, 412.0, Double.NaN, Double.PositiveInfinity})
            {
                try { manual.MoveTo(AxisRole.InkCarX, target, CancellationToken.None); throw new Exception("out-of-range accepted"); }
                catch (ArgumentOutOfRangeException) { }
            }
            Check(hardware.Calls.Count == 0, "invalid target emits no command");
            manual.Home(AxisRole.InkCarX, CancellationToken.None);
            manual.MoveTo(AxisRole.InkCarX, 370, CancellationToken.None);
            Check(hardware.Calls.Contains("move:1:0") && hardware.Calls.Contains("move:1:370"), "X origin and maximum are reachable");
            manual.MoveTo(AxisRole.InkCarX, profile.InkCarMoisturizingPositionMm, CancellationToken.None);
            Check(hardware.Calls.Contains("move:1:20"), "moisturizing position is inside mechanical travel");
            hardware.Calls.Clear();
            manual.ParkInkCar(CancellationToken.None);
            Check(hardware.Calls.Contains("move:1:118"), "shared park");
            Check(RasterValidator.Validate(profile, profile.RasterWidthPixels, profile.RasterHeightPixels).Count == 0, "single pass raster fits");
            Check(RasterValidator.Validate(profile, profile.RasterWidthPixels, 1025).Count > 0, "oversize raster rejected");
            string meteorRaster = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".png");
            try
            {
                using (var bitmap = new Bitmap(32, 1))
                {
                    using (Graphics graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.White);
                    bitmap.SetPixel(0, 0, Color.Black);
                    bitmap.Save(meteorRaster, System.Drawing.Imaging.ImageFormat.Png);
                }
                uint[] imageCommand = MeteorControllerSession.BuildImageCommand(meteorRaster, 32, 1, 123, CancellationToken.None);
                Check(imageCommand.Length == 7 && imageCommand[0] == 0x7A5535A4 && imageCommand[1] == 5
                    && imageCommand[2] == 1 && imageCommand[3] == 123 && imageCommand[4] == 0 && imageCommand[5] == 32,
                    "Meteor IMAGE header");
                Check(imageCommand[6] == 0x80000000u, "Meteor 1bpp black-pixel packing");
            }
            finally { if (System.IO.File.Exists(meteorRaster)) System.IO.File.Delete(meteorRaster); }

            profile.DirectionMode = SinglePassDirectionMode.PrintOutboundOnly;
            profile.GetOutput(OutputRole.PowderFeedEnable).Channel = 11;
            profile.GetOutput(OutputRole.VacuumEnable).Channel = 4;
            Check(profile.Validate().Count == 0, "configured profile");
            var request = new LayerPrintRequest { RasterWidthPixels = profile.RasterWidthPixels, RasterHeightPixels = profile.RasterHeightPixels,
                LayerThicknessMm = 0.1, PowderFeedDurationMs = 1, PowderCarPositionMm = 255, InfraredCureDurationMs = 1 };
            hardware = new Hardware();
            new LayerPrintWorkflow(profile, hardware, hardware, hardware).PrintLayer(request, CancellationToken.None);
            Check(hardware.Calls.IndexOf("do:11:False") < hardware.Calls.IndexOf("move:7:255"), "feed closes before departure");
            Check(hardware.Calls.Count(call => call.StartsWith("print:")) == 1 && hardware.Calls.Contains("print:out"), "outbound only");
            Check(hardware.Calls.Last(call => call.StartsWith("move:1:")) == "move:1:118", "empty return parks");
            profile.DirectionMode = SinglePassDirectionMode.PrintBothDirections;
            var frozenPlan = new SinglePassScanPlan(profile);
            profile.DirectionMode = SinglePassDirectionMode.PrintOutboundOnly;
            Check(frozenPlan.ScanCount == 2 && frozenPlan.Start(0) == 118 && frozenPlan.End(0) == 370 && frozenPlan.End(1) == 118, "plan snapshot and directions");
            Check(frozenPlan.PrintOffsetMm == 76, "centered 100mm window inside 252mm scan travel");
            profile.DirectionMode = SinglePassDirectionMode.PrintBothDirections;
            hardware = new Hardware();
            new LayerPrintWorkflow(profile, hardware, hardware, hardware).PrintLayer(request, CancellationToken.None);
            Check(hardware.Calls.Count(call => call.StartsWith("print:")) == 2 && hardware.Calls.Contains("print:back"), "round trip repeats same layer in reverse direction");
            Check(hardware.Calls.IndexOf("scan-complete") < hardware.Calls.IndexOf("print:back"), "return waits for outbound completion");
            Check(hardware.Calls.Last(call => call.StartsWith("move:1:")) == "move:1:118", "round trip parks");
            using (var cancel = new CancellationTokenSource())
            {
                hardware = new Hardware { CancelOnFeed = cancel };
                try { new LayerPrintWorkflow(profile, hardware, hardware, hardware).PrintLayer(request, cancel.Token); throw new Exception("cancel ignored"); }
                catch (OperationCanceledException) { }
                Check(hardware.Calls.Contains("do:11:False") && hardware.Calls.Contains("outputs-off"), "cancel closes feed");
                Check(!hardware.Calls.Any(call => call.StartsWith("print:")), "cancel prevents print");
            }
            string temporary = System.IO.Path.GetTempFileName();
            try
            {
                profile.Save(temporary);
                var saved = SmallPrinterProfile.Load(temporary);
                Check(saved.DirectionMode == SinglePassDirectionMode.PrintBothDirections && saved.GetOutput(OutputRole.PowderFeedEnable).Channel == 11, "mode and IO persist");
            }
            finally { System.IO.File.Delete(temporary); }

            var levels = new List<short>();
            var originalWriter = PowderFeedOutput.WriteOutput;
            try
            {
                PowderFeedOutput.WriteOutput = (card, type, channel, level) => { Check(channel == 11, "native bit index"); levels.Add(level); return 0; };
                PowderFeedOutput.Feed(1);
                Check(levels.SequenceEqual(new short[] { 0, 1 }) && !PowderFeedOutput.IsOpen, "energize-open / deenergize-close");
                Exception feedError = null;
                using (var opened = new ManualResetEventSlim())
                {
                    PowderFeedOutput.WriteOutput = (card, type, channel, level) => { if (level == 0) opened.Set(); return 0; };
                    var worker = new Thread(() => { try { PowderFeedOutput.Feed(10000); } catch (Exception ex) { feedError = ex; } });
                    worker.Start();
                    Check(opened.Wait(1000), "feed opens");
                    PowderFeedOutput.Set(false);
                    Check(worker.Join(1000) && feedError is OperationCanceledException && !PowderFeedOutput.IsOpen, "manual stop cancels timed feed");
                }
            }
            finally { PowderFeedOutput.WriteOutput = originalWriter; }

            BinderJetting.SmallMachineConfiguration.ResetLayer();
            BinderJetting.SmallMachineConfiguration.DataQueued(0);
            BinderJetting.SmallMachineConfiguration.WaitData(0);
            BinderJetting.SmallMachineConfiguration.MotionFinished(0);
            BinderJetting.SmallMachineConfiguration.WaitMotion(0);
            BinderJetting.SmallMachineConfiguration.DataLayerFinished();
            BinderJetting.SmallMachineConfiguration.ResetLayer();
            BinderJetting.SmallMachineConfiguration.Fail(new OperationCanceledException());
            try { BinderJetting.SmallMachineConfiguration.WaitData(1); throw new Exception("failed gate accepted"); }
            catch (InvalidOperationException) { }
            return "PASS: cleaning positions/timing, scraper conversion, press-ink DO13, outbound/round-trip plans, Meteor IMAGE packing, travel/raster limits, EXO10/API11 levels, timed-feed cancellation, park and scan handshake.";
        }
    }
}
