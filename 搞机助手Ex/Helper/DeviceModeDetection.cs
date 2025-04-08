using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace 搞机助手Ex.Helper
{
    public partial class ADBClient
    {
        // FastbootClient 实例
        private FastbootClient _fastbootClient;

        // 保持原始路径不带引号
        private string fastbootPath = "Tools\\fastboot.exe";

        /// <summary>
        /// 获取或设置fastboot可执行文件的路径
        /// </summary>
        public string FastbootPath
        {
            get { return fastbootPath; }
            set
            {
                if (File.Exists(value))
                {
                    fastbootPath = value;
                    // 更新FastbootClient的路径
                    if (_fastbootClient != null)
                        _fastbootClient.FastbootPath = value;
                }
                // 不自动添加引号，避免路径处理混乱
            }
        }

        /// <summary>
        /// 获取FastbootClient实例
        /// </summary>
        public FastbootClient FastbootClient
        {
            get
            {
                if (_fastbootClient == null)
                {
                    _fastbootClient = new FastbootClient(fastbootPath);
                }
                return _fastbootClient;
            }
        }

        /// <summary>
        /// 检测连接设备的当前模式
        /// </summary>
        /// <returns>检测到的设备模式</returns>
        public async Task<DeviceMode> DetectDeviceModeAsync(CancellationToken cancellationToken = default)
        {
            // 首先检查是否有任何通过ADB连接的设备
            var devicesResult = await ExecuteCommandAsync("devices", cancellationToken);
            var devices = new List<Device>();

            if (devicesResult.Success)
            {
                string[] outLines = devicesResult.Output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                // 跳过第一行 "List of devices attached"
                foreach (var line in outLines.Skip(1))
                {
                    // 使用更宽松的分隔符，因为不同ADB版本可能使用空格或制表符
                    var parts = line.Trim().Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        devices.Add(new Device { Id = parts[0], State = parts[1] });
                    }
                }
            }

            if (devices.Count == 0)
            {
                // 如果没有ADB设备，检查fastboot设备
                // 使用FastbootClient来检测fastboot设备
                var fastbootDevices = await FastbootClient.GetDevicesAsync(cancellationToken);

                // 记录fastboot输出以便调试
                Debug.WriteLine($"Fastboot设备数量: {fastbootDevices.Count}");

                // 如果检测到任何fastboot设备，则返回Fastboot模式
                if (fastbootDevices.Count > 0)
                {
                    return DeviceMode.Fastboot;
                }

                // 检查三星下载模式（Odin模式）
                // 这需要安装Samsung驱动程序
                var samsungResult = await ExecuteCommandAsync("\"" + AdbPath + "\" devices", cancellationToken);
                if (samsungResult.Success && samsungResult.Output.Contains("download"))
                {
                    return DeviceMode.Download;
                }

                return DeviceMode.NoDevice;
            }

            // 设备通过ADB连接，直接通过设备状态确定模式
            var device = devices.FirstOrDefault();
            if (device != null)
            {
                // 直接检查设备状态字符串
                string state = device.State.ToLower().Trim();

                // 根据设备状态返回对应的模式
                switch (state)
                {
                    case "recovery":
                        return DeviceMode.Recovery;

                    case "device":
                        return DeviceMode.System;

                    case "sideload":
                        // sideload通常也是Recovery模式的一种特殊状态
                        return DeviceMode.Recovery;

                    default:
                        // 如果状态不明确，可以尝试之前的检测方法作为备用
                        if (await IsInRecoveryModeAsync(cancellationToken))
                        {
                            return DeviceMode.Recovery;
                        }
                        return DeviceMode.Unknown;
                }
            }

            return DeviceMode.Unknown;
        }

        /// <summary>
        /// 使用多种方法检测设备是否处于Recovery模式
        /// </summary>
        private async Task<bool> IsInRecoveryModeAsync(CancellationToken cancellationToken = default)
        {
            // 检查recovery特有进程
            var processResult = await ExecuteShellCommandAsync("ps | grep -E 'recovery|twrp'", false, cancellationToken);
            if (processResult.Success && !string.IsNullOrWhiteSpace(processResult.Output) &&
                !processResult.Output.Contains("grep"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取当前设备模式的字符串表示
        /// </summary>
        public async Task<string> GetDeviceModeStringAsync(CancellationToken cancellationToken = default)
        {
            var mode = await DetectDeviceModeAsync(cancellationToken);

            switch (mode)
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
}