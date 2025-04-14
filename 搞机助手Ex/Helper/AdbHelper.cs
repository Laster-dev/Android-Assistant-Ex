using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Windows;

namespace 搞机助手Ex.Helper
{
    public partial class ADBClient : IDisposable
    {
        // ADB path configuration
        public string AdbPath { get; private set; } = "Tools\\adb";

        // Command timeout configuration
        public int CommandTimeout { get; set; } = 5000;

        // SemaphoreSlim for thread-safe command execution
        private readonly SemaphoreSlim _commandLock = new SemaphoreSlim(1, 1);

        private bool _serverInitialized = false;

        public ADBClient()
        {
            _ = InitializeAdbServerAsync();
        }

        public ADBClient(string adbPath)
        {
            if (File.Exists(adbPath))
                AdbPath = adbPath;

            _ = InitializeAdbServerAsync();
        }

        private async Task InitializeAdbServerAsync()
        {
            if (_serverInitialized)
                return;

            // Start the ADB server once
            var startInfo = new ProcessStartInfo
            {
                FileName = AdbPath,
                Arguments = "start-server",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            // 使用Task.Run将进程启动和管理移至后台线程
            await Task.Run(() =>
            {
                using (var process = Process.Start(startInfo))
                {
                    // 我们不等待进程完成，但仍然需要初始化它
                    // 可以选择性地读取一些输出以确认进程已正确启动
                    string output = process.StandardOutput.ReadLine();
                    // 根据需要处理输出
                }

                _serverInitialized = true;
            });
        }

        #region Core Command Execution

        /// <summary>
        /// Executes an ADB command asynchronously
        /// </summary>
        public async Task<CommandResult> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default, bool infiniteWait = false)
        {
            try
            {
                await _commandLock.WaitAsync(cancellationToken);

                // 确保服务器已启动
                if (!_serverInitialized)
                {
                    _ = InitializeAdbServerAsync();
                }

                var result = new CommandResult();

                var startInfo = new ProcessStartInfo
                {
                    FileName = AdbPath,
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    StandardOutputEncoding = Encoding.UTF8,  // Set UTF-8 encoding
                    StandardErrorEncoding = Encoding.UTF8    // Set UTF-8 encoding
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    var outputBuilder = new StringBuilder();
                    var errorBuilder = new StringBuilder();

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                            outputBuilder.AppendLine(e.Data);
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                            errorBuilder.AppendLine(e.Data);
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // 根据infiniteWait参数决定是否使用无限等待
                    bool completed;
                    if (infiniteWait)
                    {
                        // 无限等待进程完成，但仍然尊重取消令牌
                        await Task.Run(() =>
                        {
                            process.WaitForExit();
                        }, cancellationToken);
                        completed = true;
                    }
                    else
                    {
                        // 使用原有的超时逻辑
                        completed = await Task.Run(() =>
                        {
                            return process.WaitForExit(CommandTimeout);
                        }, cancellationToken);
                    }

                    if (!completed)
                    {
                        try { process.Kill(); } catch { }
                        result.Success = false;
                        result.ErrorMessage = "命令执行超时";
                    }
                    else
                    {
                        result.Success = process.ExitCode == 0;
                        result.Output = outputBuilder.ToString().TrimEnd();
                        result.ErrorMessage = errorBuilder.ToString().TrimEnd();
                    }
                }

                return result;
            }
            finally
            {
                _commandLock.Release();
            }
        }

        /// <summary>
        /// Executes an ADB command synchronously (wrapper around async method)
        /// </summary>
        public CommandResult ExecuteCommand(string command, bool infiniteWait = false)
        {
            return ExecuteCommandAsync(command, default, infiniteWait).GetAwaiter().GetResult();
        }

        // This method is no longer needed as we're calling ADB directly
        // private string FormatAdbCommand(string command)
        // {
        //    return $"\"{AdbPath}\" {command}";
        // }

        #endregion

        #region Device Connection Management

        public async Task<CommandResult> ConnectAsync(string ip, CancellationToken cancellationToken = default)
        {
            return await ExecuteCommandAsync($"connect {ip}", cancellationToken);
        }

        public async Task<CommandResult> DisconnectAsync(string ip, CancellationToken cancellationToken = default)
        {
            return await ExecuteCommandAsync($"disconnect {ip}", cancellationToken);
        }

        public async Task<CommandResult> StartServerAsync(CancellationToken cancellationToken = default)
        {
            _serverInitialized = false;
            return await ExecuteCommandAsync("start-server", cancellationToken);
        }

        public async Task<CommandResult> KillServerAsync(CancellationToken cancellationToken = default)
        {
            _serverInitialized = false;
            return await ExecuteCommandAsync("kill-server", cancellationToken);
        }

        public async Task<List<Device>> GetDevicesAsync(CancellationToken cancellationToken = default)
        {
            var result = await ExecuteCommandAsync("devices", cancellationToken);

            if (!result.Success)
                return new List<Device>();

            var devices = new List<Device>();
            var lines = result.Output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            // Skip the first line which is just "List of devices attached"
            foreach (var line in lines.Skip(1))
            {
                var parts = line.Trim().Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    devices.Add(new Device { Id = parts[0], State = parts[1] });
                }
            }

            return devices;
        }

        #endregion

        #region Device Shell Commands

        public async Task<CommandResult> ExecuteShellCommandAsync(string command, bool asRoot = false, CancellationToken cancellationToken = default)
        {
            string shellCommand = asRoot
                ? $"shell su -c \"{command}\""
                : $"shell {command}";

            return await ExecuteCommandAsync(shellCommand, cancellationToken);
        }

        public async Task<CommandResult> RemountAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteShellCommandAsync("mount -o rw,remount /system", true, cancellationToken);
        }

        public async Task<CommandResult> RebootAsync(BootState bootState = BootState.System, CancellationToken cancellationToken = default)
        {
            string rebootCommand;
            switch (bootState)
            {
                case BootState.Bootloader:
                    rebootCommand = "reboot bootloader";
                    break;
                case BootState.Recovery:
                    rebootCommand = "reboot recovery";
                    break;
                case BootState.Poweroff:
                    rebootCommand = "reboot -p";
                    break;
                default:
                    rebootCommand = "reboot";
                    break;
            }

            return await ExecuteShellCommandAsync(rebootCommand, false, cancellationToken);
        }

        #endregion

        #region File Operations

        public async Task<CommandResult> PushAsync(string localPath, string remotePath, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteCommandAsync($"push \"{localPath}\" \"{remotePath}\"", cancellationToken);
            }
            catch
            {
                // Try with alternate path format (backslashes instead of forward slashes)
                return await ExecuteCommandAsync($"push \"{localPath.Replace('/', '\\')}\" \"{remotePath}\"", cancellationToken);
            }
        }

        public async Task<CommandResult> PullAsync(string remotePath, string localPath = null, CancellationToken cancellationToken = default)
        {
            try
            {
                string command = string.IsNullOrWhiteSpace(localPath)
                    ? $"pull \"{remotePath}\""
                    : $"pull \"{remotePath}\" \"{localPath}\"";

                return await ExecuteCommandAsync(command, cancellationToken);
            }
            catch
            {
                // Try with alternate path format if a local path is specified
                if (!string.IsNullOrWhiteSpace(localPath))
                {
                    return await ExecuteCommandAsync($"pull \"{remotePath}\" \"{localPath.Replace('/', '\\')}\"", cancellationToken);
                }
                throw;
            }
        }

        #endregion

        #region App Management

        // 获取应用包名的类型（支持多选）
        [Flags]
        public enum PackageFilterType
        {
            All = 0,          // 全部
            ThirdParty = 1,   // 第三方应用
            System = 2,       // 系统应用
            Frozen = 4,       // 已冻结的应用
            Unfrozen = 8      // 未冻结的应用
        }

        // 修改后的方法
        public async Task<List<string>> GetAllPackageNamesAsync(PackageFilterType filterType = PackageFilterType.All, CancellationToken cancellationToken = default)
        {
            string commands = "pm list packages";

            // 构建筛选条件的命令
            if (filterType.HasFlag(PackageFilterType.ThirdParty))
                commands+=(" -3");
            if (filterType.HasFlag(PackageFilterType.System))
                commands += (" -s");
            if (filterType.HasFlag(PackageFilterType.Frozen))
                commands += (" -d"); // 假设存在冻结相关命令
            if (filterType.HasFlag(PackageFilterType.Unfrozen))
                commands += (" -e"); // 假设存在未冻结相关命令

            // 如果没有明确的筛选条件，则获取全部

            var results = new List<string>();

            // 执行所有命令并合并结果

                var result = await ExecuteShellCommandAsync(commands, false, cancellationToken);
                if (result.Success)
                {
                    results.AddRange(result.Output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Trim().Replace("package:", "")));
                }
            

            // 去重，返回最终结果
            return results.Distinct().ToList();
        }

        public async Task<CommandResult> InstallAppAsync(string appPath, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteCommandAsync($"install \"{appPath}\"", cancellationToken);
            }
            catch
            {
                return await ExecuteCommandAsync($"install \"{appPath.Replace('/', '\\')}\"", cancellationToken);
            }
        }

        public async Task<CommandResult> UninstallAppAsync(string packageName, CancellationToken cancellationToken = default)
        {
            return await ExecuteCommandAsync($"uninstall \"{packageName}\"", cancellationToken);
        }

        //冻结应用
        public async Task<CommandResult> FreezeAppAsync(string packageName, CancellationToken cancellationToken = default)
        {
            return await ExecuteShellCommandAsync($"pm disable-user --user 0 {packageName}", false, cancellationToken);
        }
        //解冻应用
        public async Task<CommandResult> UnfreezeAppAsync(string packageName, CancellationToken cancellationToken = default)
        {
            return await ExecuteShellCommandAsync($"pm enable --user 0 {packageName}", false, cancellationToken);
        }
        #endregion

        #region Device Information

        public async Task<string> GetManufacturerAsync(CancellationToken cancellationToken = default)
        {
            var result = await ExecuteShellCommandAsync("getprop ro.product.manufacturer", false, cancellationToken);
            return result.Success ? result.Output.Trim() : string.Empty;
        }

        public async Task<string> GetModelAsync(CancellationToken cancellationToken = default)
        {
            var result = await ExecuteShellCommandAsync("getprop ro.product.model", false, cancellationToken);
            return result.Success ? result.Output.Trim() : string.Empty;
        }

        public async Task<string> GetAndroidVersionAsync(CancellationToken cancellationToken = default)
        {
            var result = await ExecuteShellCommandAsync("getprop ro.build.version.release", false, cancellationToken);
            return result.Success ? result.Output.Trim() : string.Empty;
        }

        public async Task<string> GetDeviceCodeNameAsync(CancellationToken cancellationToken = default)
        {
            var result = await ExecuteShellCommandAsync("getprop ro.product.device", false, cancellationToken);
            return result.Success ? result.Output.Trim() : string.Empty;
        }

        public async Task<string> GetSerialNumberAsync(CancellationToken cancellationToken = default)
        {
            var result = await ExecuteShellCommandAsync("getprop ro.serialno", false, cancellationToken);
            return result.Success ? result.Output.Trim() : string.Empty;
        }

        public async Task<string> GetCpuArchitectureAsync(CancellationToken cancellationToken = default)
        {
            var result = await ExecuteShellCommandAsync("getprop ro.product.cpu.abi", false, cancellationToken);
            return result.Success ? result.Output.Trim() : string.Empty;
        }

        /// <summary>
        /// 获取电池电量
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<(int BatteryLevel, int BatteryTemperature, int BatteryVoltage)> GetBatteryInfoAsync(CancellationToken cancellationToken = default)
        {
            var result = await ExecuteShellCommandAsync("dumpsys battery", false, cancellationToken);
            if (result.Success)
            {
                int batteryLevel = -1;
                int batteryTemperature = -1;
                int batteryVoltage = -1;

                var lines = result.Output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var trimmedLine = line.TrimStart();

                    if (trimmedLine.StartsWith("level:"))
                    {
                        var parts = trimmedLine.Split(':');
                        if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out var level))
                        {
                            batteryLevel = level;
                        }
                    }
                    else if (trimmedLine.StartsWith("temperature:"))
                    {
                        var parts = trimmedLine.Split(':');
                        if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out var temperature))
                        {
                            batteryTemperature = temperature;
                        }
                    }
                    else if (trimmedLine.StartsWith("voltage:"))
                    {
                        var parts = trimmedLine.Split(':');
                        if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out var voltage))
                        {
                            batteryVoltage = voltage;
                        }
                    }
                }

                return (batteryLevel, batteryTemperature, batteryVoltage);
            }

            return (-1, -1, -1); // Return -1 for all values to indicate failure
        }


        #endregion

        #region Backup and Restore

        public async Task<CommandResult> BackupAsync(string backupPath, string backupArgs = null, CancellationToken cancellationToken = default)
        {
            string command = string.IsNullOrWhiteSpace(backupArgs)
                ? $"backup \"{backupPath}\""
                : $"backup \"{backupPath}\" \"{backupArgs}\"";

            return await ExecuteCommandAsync(command, cancellationToken);
        }

        public async Task<CommandResult> RestoreAsync(string backupPath, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteCommandAsync($"restore \"{backupPath}\"", cancellationToken);
            }
            catch
            {
                return await ExecuteCommandAsync($"restore \"{backupPath.Replace('/', '\\')}\"", cancellationToken);
            }
        }

        #endregion

        #region Logging

        public async Task<CommandResult> StartLogcatAsync(string logPath, bool overWrite = false, CancellationToken cancellationToken = default)
        {
            string redirectOperator = overWrite ? ">" : ">>";
            try
            {
                return await ExecuteCommandAsync($"logcat {redirectOperator} \"{logPath}\"", cancellationToken);
            }
            catch
            {
                return await ExecuteCommandAsync($"logcat {redirectOperator} \"{logPath.Replace('/', '\\')}\"", cancellationToken);
            }
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            _commandLock?.Dispose();

            // Ensure ADB server is properly terminated
            try
            {
                ExecuteCommand("kill-server");
            }
            catch { }
        }

        #endregion
    }

    public class CommandResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class Device
    {
        public string Id { get; set; }
        public string State { get; set; }
    }

    public enum BootState
    {
        System,
        Bootloader,
        Recovery,
        Poweroff
    }
}