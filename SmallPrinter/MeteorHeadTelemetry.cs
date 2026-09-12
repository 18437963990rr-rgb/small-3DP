using System;
using System.IO;
using System.Reflection;

namespace LaserAdd.SmallPrinter
{
    public sealed class HeadTelemetrySnapshot
    {
        public double TemperatureC { get; set; }
        public bool HasVoltage { get; set; }
        public double VoltageV { get; set; }
        public string Source { get; set; }
    }

    public sealed class MeteorMaintenanceSnapshot
    {
        public int PccStatusBits { get; set; }
        public int HeadStatusBits { get; set; }
        public int HeadState { get; set; }
        public double AuxiliaryTemperatureC { get; set; }
        public double AmplifierTemperatureC { get; set; }
    }

    /// <summary>
    /// 运行时加载 Meteor 的 PrinterInterfaceCLS，不取得打印机连接所有权，只读取 HDC 状态。
    /// Meteor 的电压状态字段会随喷头型号改变，因此电压字段和倍率由设备配置指定。
    /// </summary>
    public sealed class MeteorHeadTelemetryReader
    {
        private readonly MeteorTelemetryProfile _profile;
        private Type _apiType;
        private MethodInfo _getHeadStatus;
        private string _initializationError;

        public MeteorHeadTelemetryReader(MeteorTelemetryProfile profile)
        {
            _profile = profile;
        }

        public bool TryRead(out HeadTelemetrySnapshot snapshot, out string error)
        {
            snapshot = null;
            if (_profile == null || !_profile.Enabled)
            {
                error = "Meteor 遥测未启用";
                return false;
            }

            if (!EnsureApi(out error))
                return false;

            try
            {
                object status;
                if (!TryGetHeadStatus(out status, out error))
                    return false;
                int rawTemperature = ReadInteger(status, _profile.TemperatureField);
                int rawVoltage = ReadInteger(status, _profile.VoltageField);
                snapshot = new HeadTelemetrySnapshot
                {
                    TemperatureC = rawTemperature * _profile.TemperatureScale,
                    HasVoltage = !String.IsNullOrWhiteSpace(_profile.VoltageField),
                    VoltageV = rawVoltage * _profile.VoltageScale,
                    Source = "Meteor PCC " + _profile.PccNumber + " / HDC " + _profile.HeadNumber
                };
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = "Meteor 状态读取失败：" + exception.GetBaseException().Message;
                return false;
            }
        }

        public bool TryReadMaintenance(out MeteorMaintenanceSnapshot snapshot, out string error)
        {
            snapshot = null;
            if (!EnsureApi(out error)) return false;
            try
            {
                object headStatus, pccStatus;
                if (!TryGetHeadStatus(out headStatus, out error) || !TryGetPccStatus(out pccStatus, out error)) return false;
                snapshot = new MeteorMaintenanceSnapshot
                {
                    PccStatusBits = ReadInteger(pccStatus, "bmStatusBits"),
                    HeadStatusBits = ReadInteger(headStatus, "bmStatusBits"),
                    HeadState = ReadInteger(headStatus, "HeadState"),
                    AuxiliaryTemperatureC = ReadInteger(headStatus, "Temperature2") * 0.1,
                    AmplifierTemperatureC = ReadInteger(headStatus, "Temperature3") * 0.1
                };
                error = null;
                return true;
            }
            catch (Exception exception) { error = "Meteor 维护状态读取失败：" + exception.GetBaseException().Message; return false; }
        }

        public bool TrySetHeadPower(bool enabled, out string error)
        {
            if (!EnsureApi(out error)) return false;
            try
            {
                MethodInfo method = FindMethod("PiSetHeadPower", 1);
                if (method == null) throw new MissingMethodException("PrinterInterfaceCLS 中未找到 PiSetHeadPower。");
                object result = method.Invoke(null, new[] { ConvertParameter(method.GetParameters()[0].ParameterType, enabled ? 1 : 0) });
                if (!IsOk(result)) { error = "PiSetHeadPower 返回 " + result; return false; }
                error = null; return true;
            }
            catch (Exception exception) { error = "喷头" + (enabled ? "上电" : "断电") + "失败：" + exception.GetBaseException().Message; return false; }
        }

        public bool TrySpit(out string error)
        {
            if (!EnsureApi(out error)) return false;
            try
            {
                MethodInfo method = FindMethod("PiSetSignal", 2);
                if (method == null) throw new MissingMethodException("PrinterInterfaceCLS 中未找到 PiSetSignal。");
                int count = _profile.SpitCount <= 0 ? 200 : _profile.SpitCount;
                ParameterInfo[] parameters = method.GetParameters();
                object result = method.Invoke(null, new[] { ConvertParameter(parameters[0].ParameterType, 0x0B), ConvertParameter(parameters[1].ParameterType, count) });
                if (!IsOk(result)) { error = "PiSetSignal(SIG_SPIT) 返回 " + result; return false; }
                error = null; return true;
            }
            catch (Exception exception) { error = "闪喷失败：" + exception.GetBaseException().Message; return false; }
        }

        private bool EnsureApi(out string error)
        {
            if (_apiType != null && _getHeadStatus != null)
            {
                error = null;
                return true;
            }
            if (!String.IsNullOrEmpty(_initializationError))
            {
                error = _initializationError;
                return false;
            }

            try
            {
                _apiType = Type.GetType("Ttp.Meteor.PrinterInterfaceCLS, PrinterInterfaceCLS", false);
                if (_apiType == null && !String.IsNullOrWhiteSpace(_profile.ApiDirectory))
                {
                    string path = Path.Combine(_profile.ApiDirectory, "PrinterInterfaceCLS.dll");
                    if (File.Exists(path))
                        _apiType = Assembly.LoadFrom(path).GetType("Ttp.Meteor.PrinterInterfaceCLS", false);
                }
                if (_apiType == null)
                    throw new FileNotFoundException("未找到 PrinterInterfaceCLS.dll。请在配置中指定 MeteorTelemetry.ApiDirectory，或将其部署到程序目录。");

                _getHeadStatus = _apiType.GetMethod("PiGetHeadStatus", BindingFlags.Public | BindingFlags.Static);
                if (_getHeadStatus == null)
                    throw new MissingMethodException("PrinterInterfaceCLS 中未找到 PiGetHeadStatus。");
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                _initializationError = "Meteor API 不可用：" + exception.GetBaseException().Message;
                error = _initializationError;
                return false;
            }
        }

        private bool TryGetHeadStatus(out object status, out string error)
        {
            status = null;
            object[] args = { _profile.PccNumber, _profile.HeadNumber, null };
            object result = _getHeadStatus.Invoke(null, args);
            if (!IsOk(result)) { error = "PiGetHeadStatus 返回 " + (result == null ? "空结果" : result.ToString()); return false; }
            status = args[2]; error = null; return true;
        }

        private bool TryGetPccStatus(out object status, out string error)
        {
            status = null;
            MethodInfo method = FindMethod("PiGetPccStatus", 2);
            if (method == null) { error = "PrinterInterfaceCLS 中未找到 PiGetPccStatus。"; return false; }
            object[] args = { ConvertParameter(method.GetParameters()[0].ParameterType, _profile.PccNumber), null };
            object result = method.Invoke(null, args);
            if (!IsOk(result)) { error = "PiGetPccStatus 返回 " + (result == null ? "空结果" : result.ToString()); return false; }
            status = args[1]; error = null; return true;
        }

        private MethodInfo FindMethod(string name, int parameterCount)
        {
            foreach (MethodInfo method in _apiType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                if (method.Name == name && method.GetParameters().Length == parameterCount) return method;
            return null;
        }

        private static bool IsOk(object result)
        {
            return result != null && (String.Equals(result.ToString(), "RVAL_OK", StringComparison.Ordinal) || Convert.ToInt32(result) == 0);
        }

        private static object ConvertParameter(Type targetType, int value)
        {
            Type type = targetType.IsByRef ? targetType.GetElementType() : targetType;
            return Convert.ChangeType(value, type);
        }

        private static int ReadInteger(object source, string fieldName)
        {
            if (String.IsNullOrWhiteSpace(fieldName))
                return 0;

            int bracket = fieldName.IndexOf('[');
            string memberName = bracket < 0 ? fieldName : fieldName.Substring(0, bracket);
            Type type = source.GetType();
            object value = null;
            FieldInfo field = type.GetField(memberName);
            if (field != null) value = field.GetValue(source);
            else
            {
                PropertyInfo property = type.GetProperty(memberName);
                if (property != null) value = property.GetValue(source, null);
            }
            if (value == null)
                throw new MissingFieldException(type.FullName, memberName);

            if (bracket >= 0)
            {
                int close = fieldName.IndexOf(']', bracket);
                int index;
                if (close < 0 || !Int32.TryParse(fieldName.Substring(bracket + 1, close - bracket - 1), out index))
                    throw new FormatException("Meteor 字段索引格式无效：" + fieldName);
                Array values = value as Array;
                if (values == null || index < 0 || index >= values.Length)
                    throw new IndexOutOfRangeException("Meteor 字段索引超出范围：" + fieldName);
                value = values.GetValue(index);
            }
            return Convert.ToInt32(value);
        }
    }
}
