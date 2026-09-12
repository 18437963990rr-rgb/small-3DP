using System;
using System.Collections.Generic;
using System.IO;

namespace LaserAdd.SmallPrinter
{
    /// <summary>
    /// Owns the GTS controller connection used by the standalone small-printer UI.
    /// The Meteor PCC encoder is independent and is not configured here.
    /// </summary>
    public sealed class GtsControllerSession : IDisposable
    {
        private readonly short _cardNumber;

        public GtsControllerSession(short cardNumber)
        {
            _cardNumber = cardNumber;
        }

        public bool IsOpen { get; private set; }
        public bool IsInitialized { get; private set; }
        public string ConfigurationPath { get; private set; }

        public void Initialize(string configurationPath)
        {
            if (IsOpen) throw new InvalidOperationException("固高控制卡已经打开。");
            if (String.IsNullOrWhiteSpace(configurationPath)) throw new ArgumentNullException("configurationPath");

            string fullPath = Path.GetFullPath(configurationPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("未找到固高机台配置文件。", fullPath);
            ValidateOpenLoopConfiguration(fullPath);

            try
            {
                Check("GT_Open", GtsNative.GT_Open(_cardNumber, 0, 0));
                IsOpen = true;
                Check("GT_Reset", GtsNative.GT_Reset(_cardNumber));
                Check("GT_LoadConfig", GtsNative.GT_LoadConfig(_cardNumber, fullPath));

                // Axes 1-7 are pulse-command stepper axes on the small machine.
                // Axis 8 is the build-cylinder servo and retains its external feedback selection.
                for (short axis = 1; axis <= 7; axis++)
                    Check("GT_EncOff(轴 " + axis + ")", GtsNative.GT_EncOff(_cardNumber, axis));
                for (short axis = 1; axis <= 8; axis++)
                    Check("GT_ClrSts(轴 " + axis + ")", GtsNative.GT_ClrSts(_cardNumber, axis, 1));

                PowderFeedOutput.Set(false);
                ConfigurationPath = fullPath;
                IsInitialized = true;
            }
            catch
            {
                CloseAfterFailure();
                throw;
            }
        }

        private static void ValidateOpenLoopConfiguration(string path)
        {
            IDictionary<string, IDictionary<string, string>> ini = ReadIni(path);
            string controlActive = GetValue(ini, "control1", "active");
            string stepActive = GetValue(ini, "step1", "active");
            string stepAxis = GetValue(ini, "step1", "axis");
            if (controlActive != "0" || stepActive != "1" || stepAxis != "1")
                throw new InvalidDataException(
                    "机台 CFG 不符合 X 轴开环步进配置：要求 [control1] active=0，[step1] active=1、axis=1。");
        }

        private static IDictionary<string, IDictionary<string, string>> ReadIni(string path)
        {
            var result = new Dictionary<string, IDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            IDictionary<string, string> section = null;
            foreach (string sourceLine in File.ReadAllLines(path))
            {
                string line = sourceLine.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string name = line.Substring(1, line.Length - 2).Trim();
                    section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    result[name] = section;
                    continue;
                }
                int equals = line.IndexOf('=');
                if (section != null && equals > 0)
                    section[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
            }
            return result;
        }

        private static string GetValue(IDictionary<string, IDictionary<string, string>> ini, string sectionName, string key)
        {
            IDictionary<string, string> section;
            string value;
            return ini.TryGetValue(sectionName, out section) && section.TryGetValue(key, out value) ? value : null;
        }

        private static void Check(string operation, short result)
        {
            if (result != 0) throw new InvalidOperationException(operation + " 失败，固高返回码：" + result);
        }

        private void CloseAfterFailure()
        {
            IsInitialized = false;
            if (!IsOpen) return;
            try { PowderFeedOutput.Set(false); } catch { }
            try { GtsNative.GT_Close(_cardNumber); } catch { }
            IsOpen = false;
        }

        public void Dispose()
        {
            CloseAfterFailure();
        }
    }
}
