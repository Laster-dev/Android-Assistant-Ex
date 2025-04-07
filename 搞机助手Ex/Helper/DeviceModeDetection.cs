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
        // Add FastbootPath property similar to AdbPath
        private string fastbootPath = "Tools\\fastboot";

        /// <summary>
        /// Gets or sets the path to the fastboot executable
        /// </summary>
        public string FastbootPath
        {
            get { return fastbootPath; }
            set
            {
                if (File.Exists(value)) fastbootPath = value;
                else fastbootPath = "\"" + fastbootPath + "\"";
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

                // Skip the first line which is just "List of devices attached"
                foreach (var line in outLines.Skip(1))
                {
                    var parts = line.Trim().Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        devices.Add(new Device { Id = parts[0], State = parts[1] });
                    }
                }
            }

            if (devices.Count == 0)
            {
                // 如果没有ADB设备，检查fastboot设备
                var fastbootResult = await ExecuteCommandAsync($"\"{FastbootPath}\" devices", cancellationToken);
                if (fastbootResult.Success && !string.IsNullOrWhiteSpace(fastbootResult.Output) &&
                    fastbootResult.Output.Contains("\t"))
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

            // 设备通过ADB连接，现在确定它是处于正常模式还是恢复模式
            var device = devices.FirstOrDefault();
            if (device != null)
            {
                // 检查设备是否处于恢复模式
                var recoveryResult = await ExecuteShellCommandAsync("getprop ro.bootmode", false, cancellationToken);

                if (recoveryResult.Success)
                {
                    string bootMode = recoveryResult.Output.Trim().ToLower();

                    if (bootMode.Contains("recovery"))
                    {
                        return DeviceMode.Recovery;
                    }

                    // 还检查另一个可能指示恢复的属性
                    var altRecoveryResult = await ExecuteShellCommandAsync("getprop ro.boot.mode", false, cancellationToken);
                    if (altRecoveryResult.Success && altRecoveryResult.Output.Trim().ToLower().Contains("recovery"))
                    {
                        return DeviceMode.Recovery;
                    }

                    // 对TWRP和其他自定义恢复进行额外检查
                    var twrpResult = await ExecuteShellCommandAsync("ls /", false, cancellationToken);
                    if (twrpResult.Success && (
                        twrpResult.Output.Contains("twres") ||
                        twrpResult.Output.Contains("etc/twrp") ||
                        twrpResult.Output.Contains("RECOVERY")))
                    {
                        return DeviceMode.Recovery;
                    }

                    return DeviceMode.System;
                }
                else
                {
                    // 如果我们无法执行shell命令但设备已连接，
                    // 它可能处于恢复模式（某些恢复限制shell访问）
                    return DeviceMode.Recovery;
                }
            }

            return DeviceMode.Unknown;
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
                    return "正常模式";
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