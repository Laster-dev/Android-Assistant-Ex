using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace 搞机助手Ex
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private volatile bool _mainWindowOpened = false;

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
            this.Closing += (s, e) => _cts.Cancel();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 启动一个低优先级的后台任务进行初始化
            ThreadPool.QueueUserWorkItem(new WaitCallback(_ => InitializeInBackground()));
        }

        // 后台初始化主方法
        private async Task InitializeInBackground()
        {
            try
            {
                // 1. 更新状态 - 初始化
                SetStatus("正在初始化...");

                // 2. 结束相关进程
                await KillProcessesInBackground();
                SetStatus("正在准备解压工具...");

                // 3. 单独执行解压过程 (优化版)
                await ExtractToolsInBackground();

                // 4. 检查更新（如果需要）
                SetStatus("正在检查更新...");
                // await CheckUpdateAsync(); // 非 UI 代码

                // 5. 完成初始化
                SetStatus("初始化完成");

                // 6. 打开主窗口
                OpenMainWindowOnUIThread();
            }
            catch (OperationCanceledException)
            {
                // 用户取消操作，静默处理
            }
            catch (Exception ex)
            {
                ShowErrorAndOpenMainWindow($"初始化过程中发生错误: {ex.Message}");
            }
        }

        // 安全地设置状态文本（从任何线程调用）
        private void SetStatus(string status)
        {
            if (Dispatcher.CheckAccess())
            {
                // 已经在 UI 线程
                StatusText.Text = status;
            }
            else
            {
                // 从后台线程调用，需要转到 UI 线程
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = status;
                });
            }
        }

        // 结束进程的后台方法
        private async Task KillProcessesInBackground()
        {
            string[] processesToKill = { "adb", "fastboot" };

            foreach (var processName in processesToKill)
            {
                try
                {
                    Process[] processes = Process.GetProcessesByName(processName);
                    foreach (var process in processes)
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            await Task.Run(() => process.WaitForExit(1000));
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 仅记录错误，继续执行
                    SetStatus($"终止 {processName} 进程时出错: {ex.Message}");
                    await Task.Delay(200); // 让用户有时间看到错误信息
                }
            }
        }

        // 优化的解压工具方法
        // 改进的解压方法
        private async Task ExtractToolsInBackground()
        {
            try
            {
                // 路径准备
                string toolsPath = Path.Combine(Directory.GetCurrentDirectory(), "Tools");
                string zipPath = Path.Combine(Directory.GetCurrentDirectory(), "Tools.zip");

                // 1. 清理工具目录
                //SetStatus("正在清理工具目录...");

                if (Directory.Exists(toolsPath))
                {
                    return;
                }


                // 2. 准备 zip 文件
                SetStatus("正在准备资源文件...");
                await Task.Run(() =>
                {
                    if (File.Exists(zipPath)) File.Delete(zipPath);
                    using (FileStream fs = new FileStream(zipPath, FileMode.Create))
                    {
                        fs.Write(Resource1.Tools, 0, Resource1.Tools.Length);
                    }
                });

                // 3. 手动控制解压过程
                SetStatus("正在解压工具...");
                await ManuallyExtractZipFile(zipPath, toolsPath);

                SetStatus("正在清理缓存...");
                // 4. 清理 zip 文件
                await Task.Run(() =>
                {
                    if (File.Exists(zipPath)) File.Delete(zipPath);
                });
                SetStatus("清理完成...");
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                SetStatus($"解压工具时出现错误: {ex.Message}");
                await Task.Delay(1000);
                throw;
            }
        }

        // 手动控制的ZIP解压方法，确保UI不卡顿
        private async Task ManuallyExtractZipFile(string zipPath, string destinationPath)
        {
            await Task.Run(() =>
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    int totalEntries = archive.Entries.Count;
                    int processedEntries = 0;

                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        // 定期检查是否取消
                        _cts.Token.ThrowIfCancellationRequested();

                        // 构建目标文件路径
                        string destinationFilePath = Path.Combine(destinationPath, entry.FullName);
                        string destinationDirectoryPath = Path.GetDirectoryName(destinationFilePath);

                        // 确保目录存在
                        if (!string.IsNullOrEmpty(destinationDirectoryPath) && !Directory.Exists(destinationDirectoryPath))
                        {
                            Directory.CreateDirectory(destinationDirectoryPath);
                        }

                        // 跳过目录条目
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            continue;
                        }

                        // 提取文件
                        try
                        {
                            entry.ExtractToFile(destinationFilePath, true);
                        }
                        catch (IOException)
                        {
                            // 如果文件正在使用中，尝试复制流
                            try
                            {
                                using (Stream source = entry.Open())
                                using (FileStream target = File.Create(destinationFilePath))
                                {
                                    source.CopyTo(target);
                                }
                            }
                            catch
                            {
                                // 忽略单个文件错误，继续处理
                            }
                        }

                        // 更新进度
                        processedEntries++;

                        // 每处理10个文件更新一次状态
                        if (processedEntries % 10 == 0 || processedEntries == totalEntries)
                        {
                            int percentage = (int)((double)processedEntries / totalEntries * 100);
                            SetStatus($"正在解压工具... {percentage}%");
                        }

                        // *** 关键改进: 每处理一个文件后让出CPU时间 ***
                        if (processedEntries % 3 == 0) // 每3个文件暂停一次
                        {
                            Thread.Sleep(1); // 这里关键是让出CPU时间片，而不是实际延迟
                        }
                    }

                    SetStatus("解压完成");
                }
            });
        }

        // 在 UI 线程上打开主窗口
        private void OpenMainWindowOnUIThread()
        {
            if (Dispatcher.CheckAccess())
            {
                OpenMainWindow();
            }
            else
            {
                Dispatcher.Invoke(OpenMainWindow);
            }
        }

        // 显示错误并打开主窗口
        private void ShowErrorAndOpenMainWindow(string errorMessage)
        {
            if (Dispatcher.CheckAccess())
            {
                MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                if (!_mainWindowOpened) OpenMainWindow();
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    if (!_mainWindowOpened) OpenMainWindow();
                });
            }
        }

        private void OpenMainWindow()
        {
            lock (this)
            {
                if (_mainWindowOpened) return;
                _mainWindowOpened = true;
            }

            try
            {
                var mainWindow = new FormWindow();
                mainWindow.Show();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开主窗口时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }
    }
}