using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace 搞机助手Ex.Helper
{
    public class BackgroundThread : IDisposable
    {
        private readonly TextBlock _textBlock;
        public  ADBClient _adbClient;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Timer _timer;

        // Flag to track if monitoring is active
        private bool _isMonitoring;
        private bool _isCheckingDevices;

        // Default scan interval in milliseconds
        private const int DefaultScanInterval = 5000;

        public BackgroundThread(TextBlock textBlock)
        {
            _textBlock = textBlock ?? throw new ArgumentNullException(nameof(textBlock));
            _adbClient = new ADBClient();
            _cancellationTokenSource = new CancellationTokenSource();

            //MessageBox.Show("后台线程已启动");

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
                // Get devices asynchronously
                var devices = await _adbClient.GetDevicesAsync(cancellationToken);

                // Update UI on the UI thread
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (devices.Count > 0)
                    {
                        // Show the first device in the UI
                        var device = devices[0];
                        _textBlock.Text = $"搞机助手Ex    {device.Id} ({device.State})";
                    }
                    else
                    {
                        _textBlock.Text = "搞机助手Ex    No devices connected";
                    }
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
        }
    }
}