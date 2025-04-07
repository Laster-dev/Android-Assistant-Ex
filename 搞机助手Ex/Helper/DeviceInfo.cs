using System.Threading.Tasks;
using System.Threading;

namespace 搞机助手Ex.Helper
{
    public class DeviceInfo
    {
        /// <summary>
        /// 设备制造商
        /// </summary>
        public string Manufacturer { get; set; }

        /// <summary>
        /// 设备型号
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 安卓版本
        /// </summary>
        public string AndroidVersion { get; set; }

        /// <summary>
        /// 设备代号
        /// </summary>
        public string DeviceCodeName { get; set; }

        /// <summary>
        /// 序列号
        /// </summary>
        public string SerialNumber { get; set; }

        /// <summary>
        /// CPU架构
        /// </summary>
        public string CpuArchitecture { get; set; }

        /// <summary>
        /// 设备当前模式
        /// </summary>
        public DeviceMode CurrentMode { get; set; }

        /// <summary>
        /// 设备当前模式的友好名称
        /// </summary>
        public string CurrentModeString
        {
            get
            {
                // 使用传统switch语句代替C# 8.0的switch表达式
                switch (CurrentMode)
                {
                    case DeviceMode.System:
                        return "系统模式";
                    case DeviceMode.Fastboot:
                        return "Fastboot模式";
                    case DeviceMode.Recovery:
                        return "Recovery模式";
                    case DeviceMode.Download:
                        return "下载模式(奥丁模式)";
                    case DeviceMode.NoDevice:
                        return "未连接设备";
                    default:
                        return "未知模式";
                }
            }
        }

        /// <summary>
        /// 异步获取设备信息
        /// </summary>
        /// <param name="adbClient">ADB客户端实例</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>设备信息对象</returns>
        public static async Task<DeviceInfo> GetDeviceInfoAsync(ADBClient adbClient, CancellationToken cancellationToken = default)
        {
            // 首先检测设备模式
            var deviceMode = await adbClient.DetectDeviceModeAsync(cancellationToken);

            var deviceInfo = new DeviceInfo
            {
                CurrentMode = deviceMode
            };

            // 只有当设备处于正常模式或Recovery模式时才尝试获取其他设备信息
            if (deviceMode == DeviceMode.System || deviceMode == DeviceMode.Recovery)
            {
                try
                {
                    deviceInfo.Manufacturer = await adbClient.GetManufacturerAsync(cancellationToken);
                    deviceInfo.Model = await adbClient.GetModelAsync(cancellationToken);
                    deviceInfo.AndroidVersion = await adbClient.GetAndroidVersionAsync(cancellationToken);
                    deviceInfo.DeviceCodeName = await adbClient.GetDeviceCodeNameAsync(cancellationToken);
                    deviceInfo.SerialNumber = await adbClient.GetSerialNumberAsync(cancellationToken);
                    deviceInfo.CpuArchitecture = await adbClient.GetCpuArchitectureAsync(cancellationToken);
                }
                catch
                {
                    // 如果获取某些信息失败，保持这些属性为null
                }
            }
            // 如果设备处于Fastboot模式，尝试使用Fastboot命令获取一些基本信息
            else if (deviceMode == DeviceMode.Fastboot)
            {
                try
                {
                    // 创建一个FastbootClient实例
                    using (var fastbootClient = new FastbootClient())
                    {
                        // 获取Fastboot设备信息
                        var fastbootDevices = await fastbootClient.GetDevicesAsync(cancellationToken);
                        if (fastbootDevices.Count > 0)
                        {
                            deviceInfo.SerialNumber = fastbootDevices[0].SerialNumber;

                            // 尝试获取更多设备信息
                            var deviceVars = await fastbootClient.GetDeviceInfoAsync(cancellationToken);
                            if (deviceVars.ContainsKey("product"))
                                deviceInfo.Model = deviceVars["product"];
                            if (deviceVars.ContainsKey("variant"))
                                deviceInfo.DeviceCodeName = deviceVars["variant"];
                        }
                    }
                }
                catch
                {
                    // 如果获取某些信息失败，保持这些属性为null
                }
            }

            return deviceInfo;
        }

        /// <summary>
        /// 检查设备是否已连接
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return CurrentMode != DeviceMode.NoDevice && CurrentMode != DeviceMode.Unknown;
            }
        }

        /// <summary>
        /// 检查是否可以执行ADB命令
        /// </summary>
        public bool CanExecuteAdbCommands
        {
            get
            {
                return CurrentMode == DeviceMode.System || CurrentMode == DeviceMode.Recovery;
            }
        }

        /// <summary>
        /// 检查是否可以执行Fastboot命令
        /// </summary>
        public bool CanExecuteFastbootCommands
        {
            get
            {
                return CurrentMode == DeviceMode.Fastboot;
            }
        }
    }
}