using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace 搞机助手Ex.Helper
{
    public class BackgroundThread : IDisposable
    {
        private readonly TextBlock _textBlock;
        public ADBClient _adbClient;
        public FastbootClient _fastbootClient;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Timer _timer;

        // Flag to track if monitoring is active
        private bool _isMonitoring;
        private bool _isCheckingDevices;

        // Default scan interval in milliseconds
        private const int DefaultScanInterval = 2000;

        public BackgroundThread(TextBlock textBlock)
        {
            _textBlock = textBlock ?? throw new ArgumentNullException(nameof(textBlock));
            _adbClient = new ADBClient();
            _fastbootClient = new FastbootClient();
            _cancellationTokenSource = new CancellationTokenSource();

            // Start device monitoring
            StartMonitoring();
        }
        /// <summary>
        /// Starts the device monitoring process
        /// </summary>
        public void StartMonitoring()
        {
            if (_isMonitoring)
                return;

            _isMonitoring = true;
            _timer = new Timer(TimerCallback, null, 0, DefaultScanInterval);
        }

        /// <summary>
        /// Stops the device monitoring process
        /// </summary>
        public void StopMonitoring()
        {
            if (!_isMonitoring)
                return;

            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _timer?.Dispose();
            _timer = null;
            _cancellationTokenSource.Cancel();
            _isMonitoring = false;
        }

        /// <summary>
        /// Timer callback that initiates device checking
        /// </summary>
        private void TimerCallback(object state)
        {
            // Prevent overlapping executions
            if (_isCheckingDevices)
                return;

            _isCheckingDevices = true;
            try
            {
                // Run device check asynchronously, but don't await it here
                // We'll use ContinueWith to handle completion
                CheckDevicesAsync(_cancellationTokenSource.Token)
                    .ContinueWith(task =>
                    {
                        _isCheckingDevices = false;

                        if (task.IsFaulted)
                        {
                            // Log error but don't stop monitoring
                            Console.WriteLine($"Error checking devices: {task.Exception?.InnerException?.Message}");
                        }
                    });
            }
            catch (Exception ex)
            {
                _isCheckingDevices = false;
                Console.WriteLine($"Error initiating device check: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks for connected devices and updates the UI
        /// </summary>
        private async Task CheckDevicesAsync(CancellationToken cancellationToken)
        {
            try
            {
                // 使用DetectDeviceModeAsync来检测设备模式，这包括ADB和Fastboot
                var deviceMode = await _adbClient.DetectDeviceModeAsync(cancellationToken);

                // 根据检测到的设备模式，获取相应的设备详情
                string deviceInfo = "No devices connected";

                switch (deviceMode)
                {
                    case DeviceMode.System:
                    case DeviceMode.Recovery:
                        // 对于通过ADB连接的设备，获取设备ID
                        var adbDevices = await _adbClient.GetDevicesAsync(cancellationToken);
                        if (adbDevices.Count > 0)
                        {
                            var device = adbDevices[0];
                            string modeString = await _adbClient.GetDeviceModeStringAsync(cancellationToken);
                            deviceInfo = $"{device.Id} ({modeString})";
                        }
                        break;

                    case DeviceMode.Fastboot:
                        // 对于Fastboot模式的设备，获取FastBoot设备信息
                        var fastbootDevices = await _fastbootClient.GetDevicesAsync(cancellationToken);
                        if (fastbootDevices.Count > 0)
                        {
                            var device = fastbootDevices[0];
                            deviceInfo = $"{device.SerialNumber} (Fastboot模式)";
                        }
                        else
                        {
                            deviceInfo = "未知Fastboot设备";
                        }
                        break;

                    case DeviceMode.Download:
                        deviceInfo = "设备处于下载模式(奥丁模式)";
                        break;

                    case DeviceMode.NoDevice:
                        deviceInfo = "未连接设备";
                        break;

                    case DeviceMode.Unknown:
                    default:
                        deviceInfo = "未知设备状态";
                        break;
                }

                // Update UI on the UI thread
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _textBlock.Text = $"搞机助手Ex    {deviceInfo}";
                });
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, do nothing
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Device check error: {ex.Message}");
                throw; // Rethrow to be caught by the ContinueWith handler
            }
        }

        /// <summary>
        /// Manually trigger a device check
        /// </summary>
        public async Task RefreshDevicesAsync()
        {
            if (_isCheckingDevices)
                return;

            _isCheckingDevices = true;
            try
            {
                await CheckDevicesAsync(CancellationToken.None);
            }
            finally
            {
                _isCheckingDevices = false;
            }
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            StopMonitoring();
            _cancellationTokenSource.Dispose();
            _adbClient.Dispose();
            _fastbootClient.Dispose();
        }
    }
}