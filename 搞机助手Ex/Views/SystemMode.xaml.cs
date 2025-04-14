using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using 搞机助手Ex.Helper;
using 搞机助手Ex.MyControl;
using 搞机助手Ex.Windows;

namespace 搞机助手Ex.Views
{
    /// <summary>
    /// SystemMode.xaml 的交互逻辑
    /// </summary>
    public partial class SystemMode : Page
    {
        private Process _gnirehtetProcess = null; // 用于跟踪gnirehtet进程
        private bool _isSharing = false; // 网络共享状态标志

        public SystemMode()
        {
            InitializeComponent();
        }

        private async void ModernIconButton_Click(object sender, RoutedEventArgs e)
        {
            //获取按钮对象
            ModernIconButton button = sender as ModernIconButton;
            if (button == null)
            {
                return;
            }
            try
            {
                // 打开文件选择对话框
                Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "APK文件 (*.apk)|*.apk",
                    Title = "选择APK文件",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    CheckFileExists = true,
                    CheckPathExists = true,
                    Multiselect = false,
                    RestoreDirectory = true
                };

                bool? result = openFileDialog.ShowDialog();

                // 检查用户是否选择了文件
                if (result != true)
                {
                    MessageBox.Show("未选择文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 获取文件路径
                string filePath = openFileDialog.FileName;

                // UI反馈 - 显示正在安装的消息
                button.IsEnabled = false;
                button.Text = "正在安装";

                // 异步执行安装命令，避免UI冻结
                await Task.Run(() =>
                {
                    try
                    {
                        // 安装应用程序
                        string installCommand = $"install -r \"{filePath}\"";
                        CommandResult cmdstr = MainWindow.BackgroundThread._adbClient.ExecuteCommand(installCommand, true);

                        // 检查安装结果
                        if (cmdstr.Output.Contains("Success") || cmdstr.Output.Contains("success"))
                        {
                            // 安装成功
                            Application.Current.Dispatcher.Invoke(() =>
                            {

                                MessageBox.Show("应用安装成功!", "安装完成", MessageBoxButton.OK, MessageBoxImage.Information);
                            });
                        }
                        else
                        {
                            // 安装失败
                            Application.Current.Dispatcher.Invoke(() =>
                            {

                                MessageBox.Show($"安装失败: {cmdstr.Output}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {

                            MessageBox.Show($"安装过程发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"操作过程中发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 恢复按钮状态
                button.IsEnabled = true;
                button.Text = "应用安装";
            }
        }

        private async void ModernIconButton_Click_1(object sender, RoutedEventArgs e)
        {
            //推送文件
            //获取按钮对象
            ModernIconButton button = sender as ModernIconButton;
            if (button == null)
            {
                return;
            }
            try
            {
                // 打开文件选择对话框，任意文件类型均可选择
                Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "所有文件 (*.*)|*.*",
                    Title = "选择文件",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    CheckFileExists = true,
                    CheckPathExists = true,
                    Multiselect = false,
                    RestoreDirectory = true
                };

                bool? result = openFileDialog.ShowDialog();

                // 检查用户是否选择了文件
                if (result != true)
                {
                    MessageBox.Show("未选择文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 获取文件路径
                string filePath = openFileDialog.FileName;
                // 获取文件名，用于显示
                string fileName = System.IO.Path.GetFileName(filePath);

                // UI反馈 - 显示正在推送的消息
                button.IsEnabled = false;
                button.Text = "正在推送";

                // 异步执行推送命令，避免UI冻结
                await Task.Run(() =>
                {
                    try
                    {
                        //构建推送命令，推送至/sdcard/Download目录
                        string pushCommand = $"push \"{filePath}\" /sdcard/Download/";

                        // 执行ADB命令并获取结果
                        CommandResult cmdResult = MainWindow.BackgroundThread._adbClient.ExecuteCommand(pushCommand, true);

                        // 检查推送结果
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // 合并输出和错误信息进行分析，因为ADB输出通常写入到标准错误流
                            string combinedOutput = cmdResult.Output + cmdResult.ErrorMessage;

                            // 检查推送是否成功 - 关键字"file pushed"表示成功
                            if (cmdResult.Success && combinedOutput.Contains("file pushed"))
                            {
                                // 文件推送成功，提取详细信息

                                // 1. 提取推送的文件数量
                                string filesPushed = "1个文件"; // 默认值
                                Match filesMatch = Regex.Match(combinedOutput, @"(\d+)\s+file[s]?\s+pushed");
                                if (filesMatch.Success)
                                {
                                    filesPushed = filesMatch.Groups[1].Value + " 个文件";
                                }

                                // 2. 提取传输速率
                                string speedInfo = "未知";
                                Match speedMatch = Regex.Match(combinedOutput, @"(\d+(\.\d+)?)\s+(MB|KB|B)/s");
                                if (speedMatch.Success)
                                {
                                    speedInfo = speedMatch.Groups[1].Value + " " + speedMatch.Groups[3].Value + "/s";
                                }

                                // 3. 提取用时
                                string timeInfo = "未知";
                                Match timeMatch = Regex.Match(combinedOutput, @"in\s+(\d+(\.\d+)?s)");
                                if (timeMatch.Success)
                                {
                                    timeInfo = timeMatch.Groups[1].Value;
                                }

                                // 4. 提取文件大小
                                string sizeInfo = "未知";
                                Match sizeMatch = Regex.Match(combinedOutput, @"(\d+)\s+bytes");
                                if (sizeMatch.Success)
                                {
                                    int bytes = int.Parse(sizeMatch.Groups[1].Value);
                                    if (bytes > 1024 * 1024)
                                    {
                                        sizeInfo = $"{bytes / (1024.0 * 1024.0):F2} MB ({bytes} 字节)";
                                    }
                                    else if (bytes > 1024)
                                    {
                                        sizeInfo = $"{bytes / 1024.0:F2} KB ({bytes} 字节)";
                                    }
                                    else
                                    {
                                        sizeInfo = $"{bytes} 字节";
                                    }
                                }

                                MessageBox.Show($"文件{fileName}已成功推送到设备的Download目录\n" +
                                               $"推送结果：{filesPushed}\n" +
                                               $"传输速度：{speedInfo}\n" +
                                               $"用时：{timeInfo}\n" +
                                               $"文件大小：{sizeInfo}",
                                "推送成功",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                            }
                            else
                            {
                                // 推送失败
                                if (combinedOutput.Contains("error") || combinedOutput.Contains("失败") ||
                                    combinedOutput.Contains("failed") || !cmdResult.Success)
                                {
                                    MessageBox.Show($"文件推送失败：\n{combinedOutput}",
                                        "错误",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Error);
                                }
                                else
                                {
                                    // 没有明确的错误信息，但也没有明确的成功信息
                                    MessageBox.Show($"文件推送状态未知，请检查设备：\n{combinedOutput}",
                                        "警告",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Warning);
                                }
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"推送过程发生错误: {ex.Message}",
                                "错误",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"操作过程中发生错误: {ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // 恢复按钮状态
                button.IsEnabled = true;
                button.Text = "推送文件";
            }
        }

        private void ModernIconButton_Click_2(object sender, RoutedEventArgs e)
        {
            // 获取按钮对象
            ModernIconButton button = sender as ModernIconButton;
            if (button == null)
            {
                return;
            }

            try
            {
                // 检查当前共享状态
                if (_isSharing && _gnirehtetProcess != null && !_gnirehtetProcess.HasExited)
                {
                    // 如果正在共享网络，则停止共享
                    _ = StopSharing(button);
                }
                else
                {
                    // 如果没有共享，则开始共享
                    StartSharing(button);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"操作过程中发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);

                // 重置按钮状态
                button.IsEnabled = true;
                button.Text = "共享网络";
                _isSharing = false;
            }
        }

        /// <summary>
        /// 开始网络共享
        /// </summary>
        private void StartSharing(ModernIconButton button)
        {
            button.IsEnabled = false; // 禁用按钮，防止重复点击

            try
            {
                // 获取gnirehtet-run.cmd的完整路径
                string currentDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string cmdPath = System.IO.Path.Combine(currentDirectory, "Tools", "gnirehtet-run.cmd");

                // 确认文件是否存在
                if (!System.IO.File.Exists(cmdPath))
                {
                    MessageBox.Show($"找不到共享工具: {cmdPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 创建启动信息
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{cmdPath}\"",
                    WorkingDirectory = System.IO.Path.Combine(currentDirectory, "Tools"),
                    UseShellExecute = false, // 使用True可以显示命令窗口，方便用户查看输出
                    CreateNoWindow = true, // 显示窗口
                    WindowStyle = ProcessWindowStyle.Normal
                };

                // 启动进程
                _gnirehtetProcess = new Process { StartInfo = startInfo };
                _gnirehtetProcess.Start();

                // 添加进程退出事件处理
                _gnirehtetProcess.EnableRaisingEvents = true;
                _gnirehtetProcess.Exited += (s, args) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _isSharing = false;
                        button.Text = "共享网络";
                        button.IsEnabled = true;

                        // 可选：通知用户共享已停止
                        // MessageBox.Show("网络共享已停止", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                };

                // 更新状态和按钮
                _isSharing = true;
                button.Text = "停止共享";
                button.IsEnabled = true;

                // 可选：显示提示消息
                //MessageBox.Show("网络共享已启动，请勿关闭命令窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动共享失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                button.Text = "共享网络";
                button.IsEnabled = true;
                _isSharing = false;
            }
        }

        /// <summary>
        /// 停止网络共享
        /// </summary>
        private async Task StopSharing(ModernIconButton button)
        {
            button.Text = "正在停止"; // 更新按钮文本
            button.IsEnabled = false; // 禁用按钮，防止重复点击

            try
            {
                // 首先尝试优雅地关闭进程
                if (_gnirehtetProcess != null && !_gnirehtetProcess.HasExited)
                {
                    // 尝试关闭主窗口
                    _gnirehtetProcess.CloseMainWindow();

                    // 异步等待进程退出
                    bool exited = await Task.Run(() => _gnirehtetProcess.WaitForExit(3000));

                    if (!exited)
                    {
                        // 如果进程没有响应，则强制结束
                        _gnirehtetProcess.Kill();
                    }
                }

                // 另外，我们需要查找并结束所有相关进程，因为gnirehtet可能启动了多个子进程
                await Task.Run(() => KillAllGnirehtetProcesses());

                // 更新状态和按钮
                _isSharing = false;
                button.Text = "共享网络";
                button.IsEnabled = true;

                // 可选：显示提示消息
                //MessageBox.Show("网络共享已停止", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止共享失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _gnirehtetProcess = null;
                button.IsEnabled = true;
            }
        }

        /// <summary>
        /// 查找并结束所有gnirehtet相关进程
        /// </summary>
        private void KillAllGnirehtetProcesses()
        {
            try
            {
                // 查找所有可能的gnirehtet相关进程
                foreach (var processName in new[] { "gnirehtet", "adb", "java" })
                {
                    Process[] processes = Process.GetProcessesByName(processName);
                    foreach (var process in processes)
                    {
                        try
                        {
                            // 对于java和adb进程，需要进一步确认命令行参数，以免误杀其他进程
                            if (processName == "java" || processName == "adb")
                            {
                                string cmdLine = GetCommandLine(process.Id);
                                if (cmdLine != null && cmdLine.Contains("gnirehtet"))
                                {
                                    process.Kill();
                                }
                            }
                            else
                            {
                                // gnirehtet进程可以直接结束
                                process.Kill();
                            }
                        }
                        catch
                        {
                            // 忽略个别进程结束失败的情况
                        }
                    }
                }
                //结束手机的VPN-》adb shell am force-stop com.genymobile.gnirehtet
                CommandResult cmdstr = MainWindow.BackgroundThread._adbClient.ExecuteCommand("shell am force-stop com.genymobile.gnirehtet", true);
                if (cmdstr != null)
                {
                    try
                    {
                        if (!cmdstr.Success)
                        {
                            // 失败
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"手机VPN关闭失败: {cmdstr.Output}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"手机VPN关闭过程发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"结束gnirehtet相关进程时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取进程的命令行参数（需要管理员权限）
        /// </summary>
        private string GetCommandLine(int processId)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        return obj["CommandLine"]?.ToString();
                    }
                }
            }
            catch
            {
                // 可能没有足够的权限，简单忽略错误
            }
            return null;
        }

        private void ModernIconButton_Click_3(object sender, RoutedEventArgs e)
        {
            Windows_Freeze_App freeze_App = new Windows_Freeze_App();
            freeze_App.Show();
        }

        private void ModernIconButton_Click_4(object sender, RoutedEventArgs e)
        {
            //进程管理
            MessageBox.Show("别急，老铁");
        }

        private void ModernIconButton_Click_5(object sender, RoutedEventArgs e)
        {
            //性能监控
            MessageBox.Show("别急，老铁");
        }

        private void ModernIconButton_Click_6(object sender, RoutedEventArgs e)
        {
            //虚拟桌面
            Windows_Freeze_App windows_Freeze_App = new Windows_Freeze_App(1);
            windows_Freeze_App.Show();
        }

        private void ModernIconButton_Click_7(object sender, RoutedEventArgs e)
        {
            Windows_Scrcpy windows_Scrcpy = new Windows_Scrcpy();
            windows_Scrcpy.Show();
        }
    }
}
