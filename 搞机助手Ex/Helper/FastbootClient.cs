using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace 搞机助手Ex.Helper
{
    /// <summary>
    /// Client for interacting with Android devices in Fastboot mode
    /// </summary>
    public class FastbootClient : IDisposable
    {
        // Fastboot path configuration
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

        // Command timeout configuration
        public int CommandTimeout { get; set; } = 30000; // Default is longer for Fastboot, as flashing can take time

        // SemaphoreSlim for thread-safe command execution
        private readonly SemaphoreSlim _commandLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Initializes a new instance of the FastbootClient class
        /// </summary>
        public FastbootClient() { }

        /// <summary>
        /// Initializes a new instance of the FastbootClient class with specified fastboot path
        /// </summary>
        /// <param name="fastbootPath">Path to the fastboot executable</param>
        public FastbootClient(string fastbootPath)
        {
            if (File.Exists(fastbootPath))
                FastbootPath = fastbootPath;
        }

        #region Core Command Execution

        /// <summary>
        /// Executes a fastboot command asynchronously
        /// </summary>
        /// <param name="command">The fastboot command to execute</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>Result of the command execution</returns>
        public async Task<CommandResult> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            try
            {
                await _commandLock.WaitAsync(cancellationToken);

                string fullCommand = FormatFastbootCommand(command);
                var result = new CommandResult();

                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C {fullCommand}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
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

                    // Create a task that completes when the process exits
                    var processExitTask = Task.Run(() =>
                    {
                        return process.WaitForExit(CommandTimeout);
                    }, cancellationToken);

                    // Wait for the process to complete or timeout
                    bool completed = await processExitTask;

                    if (!completed)
                    {
                        try { process.Kill(); } catch { }
                        result.Success = false;
                        result.ErrorMessage = "命令执行超时";
                    }
                    else
                    {
                        // Check if output contains "FAILED" which indicates a Fastboot error
                        string output = outputBuilder.ToString();
                        string error = errorBuilder.ToString();

                        if (output.Contains("FAILED") || error.Contains("FAILED"))
                        {
                            result.Success = false;
                            result.ErrorMessage = output + "\n" + error;
                        }
                        else
                        {
                            result.Success = process.ExitCode == 0;
                            result.Output = output.TrimEnd();
                            result.ErrorMessage = error.TrimEnd();
                        }
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
        /// Executes a fastboot command synchronously
        /// </summary>
        /// <param name="command">The fastboot command to execute</param>
        /// <returns>Result of the command execution</returns>
        public CommandResult ExecuteCommand(string command)
        {
            return ExecuteCommandAsync(command).GetAwaiter().GetResult();
        }

        private string FormatFastbootCommand(string command)
        {
            return $"\"{FastbootPath}\" {command}";
        }

        #endregion

        #region Device Information

        /// <summary>
        /// Gets a list of connected devices in fastboot mode
        /// </summary>
        /// <returns>List of connected fastboot devices</returns>
        public async Task<List<FastbootDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
        {
            var result = await ExecuteCommandAsync("devices", cancellationToken);

            if (!result.Success)
                return new List<FastbootDevice>();

            var devices = new List<FastbootDevice>();
            var lines = result.Output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.Contains("fastboot") || (line.Contains("\t") && !line.StartsWith("List")))
                {
                    var parts = line.Trim().Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        devices.Add(new FastbootDevice
                        {
                            SerialNumber = parts[0],
                            State = parts.Length > 1 ? parts[1] : "fastboot"
                        });
                    }
                }
            }

            return devices;
        }

        /// <summary>
        /// Gets detailed information about the connected fastboot device
        /// </summary>
        /// <returns>Dictionary of device variables</returns>
        public async Task<Dictionary<string, string>> GetDeviceInfoAsync(CancellationToken cancellationToken = default)
        {
            var result = await ExecuteCommandAsync("getvar all", cancellationToken);

            var deviceInfo = new Dictionary<string, string>();

            if (!result.Success)
                return deviceInfo;

            var lines = result.Output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.Contains(": "))
                {
                    var parts = line.Split(new[] { ": " }, 2, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();
                        if (key.StartsWith("(bootloader) "))
                            key = key.Substring("(bootloader) ".Length);

                        deviceInfo[key] = parts[1].Trim();
                    }
                }
            }

            return deviceInfo;
        }

        /// <summary>
        /// Gets the bootloader/device unlock status
        /// </summary>
        /// <returns>True if the bootloader is unlocked</returns>
        public async Task<bool> IsBootloaderUnlockedAsync(CancellationToken cancellationToken = default)
        {
            var deviceInfo = await GetDeviceInfoAsync(cancellationToken);

            if (deviceInfo.TryGetValue("unlocked", out string unlockStatus))
            {
                return unlockStatus.Equals("yes", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        #endregion

        #region Flashing Operations

        /// <summary>
        /// Flashes a partition with the specified image file
        /// </summary>
        /// <param name="partition">Partition name (e.g., boot, system, recovery)</param>
        /// <param name="imagePath">Path to the image file</param>
        public async Task<CommandResult> FlashPartitionAsync(string partition, string imagePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(partition))
                throw new ArgumentException("分区名不能为空", nameof(partition));

            if (string.IsNullOrWhiteSpace(imagePath))
                throw new ArgumentException("镜像路径不能为空", nameof(imagePath));

            if (!File.Exists(imagePath))
                throw new FileNotFoundException("镜像文件不存在", imagePath);

            return await ExecuteCommandAsync($"flash {partition} \"{imagePath}\"", cancellationToken);
        }

        /// <summary>
        /// Erases a partition
        /// </summary>
        /// <param name="partition">Partition name to erase</param>
        public async Task<CommandResult> ErasePartitionAsync(string partition, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(partition))
                throw new ArgumentException("分区名不能为空", nameof(partition));

            return await ExecuteCommandAsync($"erase {partition}", cancellationToken);
        }

        /// <summary>
        /// Flashes a raw image to a specific partition by slot name
        /// </summary>
        /// <param name="slot">Slot name (a or b for A/B devices)</param>
        /// <param name="partition">Partition name (e.g., boot, system)</param>
        /// <param name="imagePath">Path to the image file</param>
        public async Task<CommandResult> FlashSlotAsync(string slot, string partition, string imagePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(slot))
                throw new ArgumentException("插槽名不能为空", nameof(slot));

            if (string.IsNullOrWhiteSpace(partition))
                throw new ArgumentException("分区名不能为空", nameof(partition));

            if (string.IsNullOrWhiteSpace(imagePath))
                throw new ArgumentException("镜像路径不能为空", nameof(imagePath));

            if (!File.Exists(imagePath))
                throw new FileNotFoundException("镜像文件不存在", imagePath);

            return await ExecuteCommandAsync($"flash {partition}_{slot} \"{imagePath}\"", cancellationToken);
        }

        /// <summary>
        /// Gets the current slot for A/B devices
        /// </summary>
        public async Task<string> GetCurrentSlotAsync(CancellationToken cancellationToken = default)
        {
            var result = await ExecuteCommandAsync("getvar current-slot", cancellationToken);

            if (result.Success && result.Output.Contains("current-slot:"))
            {
                // Extract the slot from output which might look like:
                // (bootloader) current-slot: a
                foreach (var line in result.Output.Split('\n'))
                {
                    if (line.Contains("current-slot:"))
                    {
                        int index = line.IndexOf("current-slot:");
                        if (index >= 0 && index + 13 < line.Length)
                        {
                            return line.Substring(index + 13).Trim();
                        }
                    }
                }
            }

            return string.Empty; // Not an A/B device or couldn't determine slot
        }

        /// <summary>
        /// Sets the active slot for A/B devices
        /// </summary>
        /// <param name="slot">Slot to set as active (a or b)</param>
        public async Task<CommandResult> SetActiveSlotAsync(string slot, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(slot))
                throw new ArgumentException("插槽名不能为空", nameof(slot));

            if (slot != "a" && slot != "b")
                throw new ArgumentException("插槽名必须为 'a' 或 'b'", nameof(slot));

            return await ExecuteCommandAsync($"--set-active={slot}", cancellationToken);
        }

        #endregion

        #region Boot Operations

        /// <summary>
        /// Boots the device with a specified kernel image
        /// </summary>
        /// <param name="kernelPath">Path to the kernel image</param>
        public async Task<CommandResult> BootImageAsync(string kernelPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(kernelPath))
                throw new ArgumentException("内核镜像路径不能为空", nameof(kernelPath));

            if (!File.Exists(kernelPath))
                throw new FileNotFoundException("内核镜像文件不存在", kernelPath);

            return await ExecuteCommandAsync($"boot \"{kernelPath}\"", cancellationToken);
        }

        /// <summary>
        /// Reboots the device to the specified mode
        /// </summary>
        /// <param name="mode">Boot mode (null for normal system, bootloader, recovery, fastboot)</param>
        public async Task<CommandResult> RebootAsync(string mode = null, CancellationToken cancellationToken = default)
        {
            string command = "reboot";

            if (!string.IsNullOrWhiteSpace(mode))
            {
                // Validate mode
                if (mode != "bootloader" && mode != "recovery" && mode != "fastboot")
                    throw new ArgumentException("无效的重启模式。有效值为: bootloader, recovery, fastboot", nameof(mode));

                command += " " + mode;
            }

            return await ExecuteCommandAsync(command, cancellationToken);
        }

        #endregion

        #region Bootloader Unlock Operations

        /// <summary>
        /// Unlocks the bootloader (device must be in OEM unlock allowed state)
        /// </summary>
        public async Task<CommandResult> UnlockBootloaderAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteCommandAsync("oem unlock", cancellationToken);
        }

        /// <summary>
        /// Locks the bootloader
        /// </summary>
        public async Task<CommandResult> LockBootloaderAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteCommandAsync("oem lock", cancellationToken);
        }

        /// <summary>
        /// Unlocks the bootloader using the device unlock code
        /// </summary>
        /// <param name="unlockCode">Unlock code provided by manufacturer</param>
        public async Task<CommandResult> UnlockBootloaderWithCodeAsync(string unlockCode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(unlockCode))
                throw new ArgumentException("解锁码不能为空", nameof(unlockCode));

            return await ExecuteCommandAsync($"oem unlock {unlockCode}", cancellationToken);
        }

        #endregion

        #region Miscellaneous Operations

        /// <summary>
        /// Updates the device with an OTA package
        /// </summary>
        /// <param name="packagePath">Path to the OTA package</param>
        public async Task<CommandResult> UpdateAsync(string packagePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
                throw new ArgumentException("OTA包路径不能为空", nameof(packagePath));

            if (!File.Exists(packagePath))
                throw new FileNotFoundException("OTA包文件不存在", packagePath);

            return await ExecuteCommandAsync($"update \"{packagePath}\"", cancellationToken);
        }

        /// <summary>
        /// Runs a custom OEM command
        /// </summary>
        /// <param name="command">OEM command to run</param>
        public async Task<CommandResult> RunOemCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("OEM命令不能为空", nameof(command));

            return await ExecuteCommandAsync($"oem {command}", cancellationToken);
        }

        /// <summary>
        /// Gets a list of partitions from the device
        /// </summary>
        public async Task<List<string>> GetPartitionsAsync(CancellationToken cancellationToken = default)
        {
            // This is a more advanced command that might not work on all devices
            var result = await ExecuteCommandAsync("getvar partition-list:all", cancellationToken);

            var partitions = new List<string>();

            if (!result.Success)
                return partitions;

            var lines = result.Output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.Contains("partition-list:"))
                {
                    int index = line.IndexOf("partition-list:");
                    if (index >= 0 && index + 15 < line.Length)
                    {
                        string partition = line.Substring(index + 15).Trim();
                        if (!string.IsNullOrEmpty(partition))
                            partitions.Add(partition);
                    }
                }
            }

            return partitions;
        }

        /// <summary>
        /// Waits for a device to connect in fastboot mode
        /// </summary>
        /// <param name="timeout">Timeout in milliseconds, -1 for indefinite</param>
        public async Task<bool> WaitForDeviceAsync(int timeout = -1, CancellationToken cancellationToken = default)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (timeout > 0)
                cts.CancelAfter(timeout);

            try
            {
                var result = await ExecuteCommandAsync("wait-for-device", cts.Token);
                return result.Success;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                cts.Dispose();
            }
        }

        #endregion

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            _commandLock?.Dispose();
        }
    }

    /// <summary>
    /// Represents a device in fastboot mode
    /// </summary>
    public class FastbootDevice
    {
        /// <summary>
        /// Serial number of the device
        /// </summary>
        public string SerialNumber { get; set; }

        /// <summary>
        /// Current state of the device (typically "fastboot")
        /// </summary>
        public string State { get; set; }
    }
}