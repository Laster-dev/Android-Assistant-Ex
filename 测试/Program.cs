using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace 搞机助手Ex.Helper
{
    public class program {

        public static async Task Main()
        {
            ScrcpyHelper scrcpyHelper = new ScrcpyHelper();
            var scrcpyAppInfos = await scrcpyHelper.GetApplicationsAsync();
            foreach (var appInfo in scrcpyAppInfos)
            {
                Console.WriteLine(appInfo.Type);
            }
        }
    }
    /// <summary>
    /// ScrcpyHelper 类，用于在Windows平台上封装Scrcpy功能
    /// </summary>
    public class ScrcpyHelper : IDisposable
    {
        #region 属性

        /// <summary>
        /// Scrcpy可执行文件路径
        /// </summary>
        public string ScrcpyPath { get; set; }

        /// <summary>
        /// ADB可执行文件路径
        /// </summary>
        public string AdbPath { get; set; }

        /// <summary>
        /// Scrcpy服务器路径
        /// </summary>
        public string ScrcpyServerPath { get; set; }

        /// <summary>
        /// 当前连接的设备序列号
        /// </summary>
        public string DeviceSerial { get; private set; }

        /// <summary>
        /// Scrcpy进程
        /// </summary>
        public Process _scrcpyProcess;
        /// <summary>
        /// Scrcpy窗口句柄
        /// </summary>
        public IntPtr ScrcpyWindow { get; set; }
        /// <summary>
        /// 是否已启动
        /// </summary>
        public bool IsRunning => _scrcpyProcess != null && !_scrcpyProcess.HasExited;

        /// <summary>
        /// 快捷键修饰符
        /// </summary>
        public enum ShortcutModifier
        {
            LeftAlt,
            RightAlt,
            LeftCtrl,
            RightCtrl,
            LeftSuper,
            RightSuper
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建ScrcpyHelper实例
        /// </summary>
        /// <param name="scrcpyPath">Scrcpy可执行文件路径</param>
        /// <param name="adbPath">ADB可执行文件路径（可选，默认会在scrcpyPath同目录下查找）</param>
        public ScrcpyHelper(string scrcpyPath = "Tools\\scrcpy.exe", string adbPath = null)
        {
            if (string.IsNullOrEmpty(scrcpyPath))
                throw new ArgumentNullException(nameof(scrcpyPath), "Scrcpy路径不能为空");

            if (!File.Exists(scrcpyPath))
                throw new FileNotFoundException("未找到Scrcpy可执行文件", scrcpyPath);

            ScrcpyPath = scrcpyPath;

            // 如果没有提供ADB路径，则假设它在scrcpy同一目录下
            if (string.IsNullOrEmpty(adbPath))
            {
                adbPath = Path.Combine(Path.GetDirectoryName(scrcpyPath), "adb.exe");
                if (!File.Exists(adbPath))
                    throw new FileNotFoundException("未找到ADB可执行文件", adbPath);
            }

            AdbPath = adbPath;
        }

        #endregion

        #region ADB设备管理

        /// <summary>
        /// 获取已连接的设备列表
        /// </summary>
        /// <returns>设备ID列表</returns>
        public async Task<List<string>> GetDevicesAsync()
        {
            var result = await ExecuteAdbCommandAsync("devices");
            var devices = new List<string>();

            var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < lines.Length; i++) // 跳过第一行（标题行）
            {
                var line = lines[i].Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && parts[1] == "device")
                    {
                        devices.Add(parts[0]);
                    }
                }
            }

            return devices;
        }

        /// <summary>
        /// 通过IP地址连接设备
        /// </summary>
        /// <param name="ipAddress">设备IP地址</param>
        /// <param name="port">端口号（默认5555）</param>
        /// <returns>连接结果</returns>
        public async Task<bool> ConnectWirelessAsync(string ipAddress, int port = 5555)
        {
            if (string.IsNullOrEmpty(ipAddress))
                throw new ArgumentNullException(nameof(ipAddress));

            // 启动无线连接
            var result = await ExecuteAdbCommandAsync($"connect {ipAddress}:{port}");
            return result.Contains("connected") || result.Contains("already connected");
        }

        /// <summary>
        /// 启用当前连接设备的TCP/IP模式
        /// </summary>
        /// <param name="port">端口号（默认5555）</param>
        /// <returns>操作结果</returns>
        public async Task<bool> EnableTcpIpModeAsync(int port = 5555)
        {
            var result = await ExecuteAdbCommandAsync($"tcpip {port}");
            return result.Contains("restarting in TCP mode") || result.Contains("success");
        }

        /// <summary>
        /// 获取连接设备的IP地址
        /// </summary>
        /// <returns>设备IP地址</returns>
        public async Task<string> GetDeviceIpAddressAsync()
        {
            var result = await ExecuteAdbCommandAsync("shell ip route | awk '{print $9}'");
            result = result.Trim();

            // 验证是否为有效IP
            if (IPAddress.TryParse(result, out _))
            {
                return result;
            }

            return null;
        }

        /// <summary>
        /// 断开无线连接
        /// </summary>
        /// <param name="ipAddress">设备IP地址</param>
        /// <param name="port">端口号（默认5555）</param>
        /// <returns>操作结果</returns>
        public async Task<bool> DisconnectWirelessAsync(string ipAddress, int port = 5555)
        {
            var result = await ExecuteAdbCommandAsync($"disconnect {ipAddress}:{port}");
            return result.Contains("disconnected");
        }

        /// <summary>
        /// 重启ADB服务
        /// </summary>
        public async Task RestartAdbServerAsync()
        {
            await ExecuteAdbCommandAsync("kill-server");
            await ExecuteAdbCommandAsync("start-server");
        }

        #endregion

        #region 启动Scrcpy

        /// <summary>
        /// 获取Scrcpy版本号
        /// </summary>
        /// <returns>版本号字符串</returns>
        public async Task<string> GetVersionAsync()
        {
            var result = await ExecuteScrcpyCommandAsync("-v");
            var match = Regex.Match(result, @"scrcpy (\d+\.\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// 启动Scrcpy基本镜像
        /// </summary>
        /// <param name="deviceSerial">设备序列号（可选，如果有多个设备则必填）</param>
        public async Task<bool> StartAsync(string deviceSerial = null)
        {
            // 构建命令
            var commandBuilder = new StringBuilder();

            // 如果指定了设备，则添加设备参数
            if (!string.IsNullOrEmpty(deviceSerial))
            {
                commandBuilder.Append($"--serial {deviceSerial} ");
                DeviceSerial = deviceSerial;
            }
            else
            {
                // 尝试获取连接的设备
                var devices = await GetDevicesAsync();
                if (devices.Count == 1)
                {
                    DeviceSerial = devices[0];
                }
                else if (devices.Count > 1)
                {
                    throw new InvalidOperationException("多个设备连接时必须指定设备序列号");
                }
                else
                {
                    throw new InvalidOperationException("没有设备连接");
                }
            }

            return await StartScrcpyWithOptionsAsync(commandBuilder.ToString());
        }

        /// <summary>
        /// 使用指定的选项启动Scrcpy
        /// </summary>
        /// <param name="options">命令行选项字符串</param>
        /// <param name="parentHandle">父窗口句柄（可选）</param>
        /// <param name="windowStyle">窗口样式（可选）</param>
        /// <param name="extendedWindowStyle">扩展窗口样式（可选）</param>
        /// <param name="x">窗口 X 坐标（可选）</param>
        /// <param name="y">窗口 Y 坐标（可选）</param>
        /// <param name="width">窗口宽度（可选）</param>
        /// <param name="height">窗口高度（可选）</param>
        /// <returns>是否成功启动</returns>
        public async Task<bool> StartScrcpyWithOptionsAsync(
            string options,
            IntPtr parentHandle = default,
            int? windowStyle = null,
            int? extendedWindowStyle = null,
            int? x = null,
            int? y = null,
            int? width = null,
            int? height = null)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Scrcpy已在运行，请先停止当前实例");
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = ScrcpyPath,
                    Arguments = options,
                    UseShellExecute = false,
                    //CreateNoWindow = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,

                };

                // 设置环境变量
                if (!string.IsNullOrEmpty(AdbPath))
                {
                    startInfo.EnvironmentVariables["ADB"] = AdbPath;
                }

                if (!string.IsNullOrEmpty(ScrcpyServerPath))
                {
                    startInfo.EnvironmentVariables["SCRCPY_SERVER_PATH"] = ScrcpyServerPath;
                }

                _scrcpyProcess = new Process { StartInfo = startInfo };
                _scrcpyProcess.EnableRaisingEvents = true;

                _scrcpyProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        OnLogReceived?.Invoke(this, e.Data);
                    }
                };

                _scrcpyProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        OnErrorReceived?.Invoke(this, e.Data);
                    }
                };

                _scrcpyProcess.Exited += (sender, e) =>
                {
                    OnProcessExited?.Invoke(this, EventArgs.Empty);
                    _scrcpyProcess = null;
                };

                _scrcpyProcess.Start();
                _scrcpyProcess.BeginOutputReadLine();
                _scrcpyProcess.BeginErrorReadLine();

                // 设置父窗口和窗口样式
                if (parentHandle != IntPtr.Zero || windowStyle.HasValue || extendedWindowStyle.HasValue ||
                    x.HasValue || y.HasValue || width.HasValue || height.HasValue)
                {
                    // 需要等待窗口创建完成
                    await Task.Delay(1000); // 等待一段时间让窗口创建出来

                    // 查找 scrcpy 的主窗口
                    ScrcpyWindow = await _scrcpyProcess.FindMainWindowAsync();

                    if (ScrcpyWindow != IntPtr.Zero)
                    {
                        // 设置窗口父窗口
                        if (parentHandle != IntPtr.Zero)
                        {

                            WindowHelper.SetWindowParent(ScrcpyWindow, parentHandle);

                        }



                        // 调整窗口位置和大小
                        if (x.HasValue || y.HasValue || width.HasValue || height.HasValue)
                        {
                            // 获取当前窗口位置和大小
                            int currentX = 0, currentY = 0, currentWidth = 800, currentHeight = 600;

                            // 使用提供的值或默认值
                            int newX = x ?? currentX;
                            int newY = y ?? currentY;
                            int newWidth = width ?? currentWidth;
                            int newHeight = height ?? currentHeight;

                            WindowHelper.MoveAndResizeWindow(ScrcpyWindow, newX, newY, newWidth, newHeight);

                        }
                        WindowHelper.SetBorderlessWindow(ScrcpyWindow);
                        // 发送通知
                        OnWindowInitialized?.Invoke(this, ScrcpyWindow);
                    }
                    else
                    {
                        OnErrorReceived?.Invoke(this, "无法找到Scrcpy窗口句柄");
                    }
                }

                // 等待确认进程是否启动成功
                await Task.Delay(500);
                return _scrcpyProcess != null && !_scrcpyProcess.HasExited;
            }
            catch (Exception ex)
            {
                OnErrorReceived?.Invoke(this, $"启动Scrcpy失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 停止Scrcpy进程
        /// </summary>
        public void Stop()
        {
            if (_scrcpyProcess != null && !_scrcpyProcess.HasExited)
            {
                try
                {
                    _scrcpyProcess.Kill();
                    _scrcpyProcess.WaitForExit(5000);
                }
                catch (Exception ex)
                {
                    OnErrorReceived?.Invoke(this, $"停止Scrcpy失败: {ex.Message}");
                }
                finally
                {
                    // _scrcpyProcess.Dispose();
                    _scrcpyProcess = null;
                }
            }
        }

        #endregion

        #region 高级选项

        /// <summary>
        /// 启动录制屏幕的Scrcpy
        /// </summary>
        /// <param name="outputFile">输出文件路径</param>
        /// <param name="bitRate">比特率（默认8M）</param>
        /// <param name="displayMirror">是否同时显示镜像（默认显示）</param>
        /// <param name="deviceSerial">设备序列号（可选）</param>
        /// <returns>是否成功启动</returns>
        public async Task<bool> StartRecordingAsync(string outputFile, string bitRate = "8M", bool displayMirror = true, string deviceSerial = null)
        {
            var options = new StringBuilder();

            options.Append($"--record \"{outputFile}\" ");
            options.Append($"--bit-rate {bitRate} ");

            if (!displayMirror)
            {
                options.Append("--no-display ");
            }

            if (!string.IsNullOrEmpty(deviceSerial))
            {
                options.Append($"--serial {deviceSerial} ");
                DeviceSerial = deviceSerial;
            }

            return await StartScrcpyWithOptionsAsync(options.ToString());
        }

        /// <summary>
        /// 自定义窗口选项启动Scrcpy
        /// </summary>
        /// <param name="title">窗口标题</param>
        /// <param name="x">窗口X坐标</param>
        /// <param name="y">窗口Y坐标</param>
        /// <param name="width">窗口宽度</param>
        /// <param name="height">窗口高度</param>
        /// <param name="borderless">是否无边框</param>
        /// <param name="alwaysOnTop">是否总在最上层</param>
        /// <param name="fullscreen">是否全屏</param>
        /// <param name="deviceSerial">设备序列号（可选）</param>
        /// <returns>是否成功启动</returns>
        public async Task<bool> StartWithCustomWindowAsync(
            string title = null,
            int? x = null,
            int? y = null,
            int? width = null,
            int? height = null,
            bool borderless = false,
            bool alwaysOnTop = false,
            bool fullscreen = false,
            string deviceSerial = null)
        {
            var options = new StringBuilder();

            if (!string.IsNullOrEmpty(title))
            {
                options.Append($"--window-title \"{title}\" ");
            }

            if (x.HasValue)
            {
                options.Append($"--window-x {x.Value} ");
            }

            if (y.HasValue)
            {
                options.Append($"--window-y {y.Value} ");
            }

            if (width.HasValue)
            {
                options.Append($"--window-width {width.Value} ");
            }

            if (height.HasValue)
            {
                options.Append($"--window-height {height.Value} ");
            }

            if (borderless)
            {
                options.Append("--window-borderless ");
            }

            if (alwaysOnTop)
            {
                options.Append("--always-on-top ");
            }

            if (fullscreen)
            {
                options.Append("--fullscreen ");
            }

            if (!string.IsNullOrEmpty(deviceSerial))
            {
                options.Append($"--serial {deviceSerial} ");
                DeviceSerial = deviceSerial;
            }

            return await StartScrcpyWithOptionsAsync(options.ToString());
        }

        /// <summary>
        /// 自定义视频选项启动Scrcpy
        /// </summary>
        /// <param name="maxSize">最大尺寸</param>
        /// <param name="bitRate">比特率</param>
        /// <param name="maxFps">最大帧率</param>
        /// <param name="crop">裁剪区域(格式：宽:高:x:y)</param>
        /// <param name="rotation">旋转角度(0, 1, 2, 3)</param>
        /// <param name="encoder">指定编码器</param>
        /// <param name="deviceSerial">设备序列号（可选）</param>
        /// <returns>是否成功启动</returns>
        public async Task<bool> StartWithCustomVideoOptionsAsync(
            string maxSize = null,
            string bitRate = null,
            int? maxFps = null,
            string crop = null,
            int? rotation = null,
            string encoder = null,
            string deviceSerial = null)
        {
            var options = new StringBuilder();

            if (!string.IsNullOrEmpty(maxSize))
            {
                options.Append($"--max-size {maxSize} ");
            }

            if (!string.IsNullOrEmpty(bitRate))
            {
                options.Append($"--bit-rate {bitRate} ");
            }

            if (maxFps.HasValue)
            {
                options.Append($"--max-fps {maxFps.Value} ");
            }

            if (!string.IsNullOrEmpty(crop))
            {
                options.Append($"--crop {crop} ");
            }

            if (rotation.HasValue && rotation >= 0 && rotation <= 3)
            {
                options.Append($"--rotation {rotation.Value} ");
            }

            if (!string.IsNullOrEmpty(encoder))
            {
                options.Append($"--encoder {encoder} ");
            }

            if (!string.IsNullOrEmpty(deviceSerial))
            {
                options.Append($"--serial {deviceSerial} ");
                DeviceSerial = deviceSerial;
            }

            return await StartScrcpyWithOptionsAsync(options.ToString());
        }

        /// <summary>
        /// 开始镜像并关闭设备屏幕
        /// </summary>
        /// <param name="deviceSerial">设备序列号（可选）</param>
        /// <returns>是否成功启动</returns>
        public async Task<bool> StartWithScreenOffAsync(string deviceSerial = null)
        {
            var options = new StringBuilder();

            options.Append("--turn-screen-off ");
            options.Append("--stay-awake ");

            if (!string.IsNullOrEmpty(deviceSerial))
            {
                options.Append($"--serial {deviceSerial} ");
                DeviceSerial = deviceSerial;
            }

            return await StartScrcpyWithOptionsAsync(options.ToString());
        }

        /// <summary>
        /// 启动显示触摸点的Scrcpy
        /// </summary>
        /// <param name="deviceSerial">设备序列号（可选）</param>
        /// <returns>是否成功启动</returns>
        public async Task<bool> StartWithShowTouchesAsync(string deviceSerial = null)
        {
            var options = new StringBuilder();

            options.Append("--show-touches ");

            if (!string.IsNullOrEmpty(deviceSerial))
            {
                options.Append($"--serial {deviceSerial} ");
                DeviceSerial = deviceSerial;
            }

            return await StartScrcpyWithOptionsAsync(options.ToString());
        }

        /// <summary>
        /// 启动只读模式的Scrcpy（不接受输入）
        /// </summary>
        /// <param name="deviceSerial">设备序列号（可选）</param>
        /// <returns>是否成功启动</returns>
        public async Task<bool> StartReadOnlyAsync(string deviceSerial = null)
        {
            var options = new StringBuilder();

            options.Append("--no-control ");

            if (!string.IsNullOrEmpty(deviceSerial))
            {
                options.Append($"--serial {deviceSerial} ");
                DeviceSerial = deviceSerial;
            }

            return await StartScrcpyWithOptionsAsync(options.ToString());
        }

        /// <summary>
        /// 设置自定义快捷键修饰符启动Scrcpy
        /// </summary>
        /// <param name="modifier">快捷键修饰符</param>
        /// <param name="deviceSerial">设备序列号（可选）</param>
        /// <returns>是否成功启动</returns>
        public async Task<bool> StartWithCustomShortcutModifierAsync(ShortcutModifier modifier, string deviceSerial = null)
        {
            var options = new StringBuilder();

            string modifierStr;
            switch (modifier)
            {
                case ShortcutModifier.LeftAlt:
                    modifierStr = "lalt";
                    break;
                case ShortcutModifier.RightAlt:
                    modifierStr = "ralt";
                    break;
                case ShortcutModifier.LeftCtrl:
                    modifierStr = "lctrl";
                    break;
                case ShortcutModifier.RightCtrl:
                    modifierStr = "rctrl";
                    break;
                case ShortcutModifier.LeftSuper:
                    modifierStr = "lsuper";
                    break;
                case ShortcutModifier.RightSuper:
                    modifierStr = "rsuper";
                    break;
                default:
                    modifierStr = "lalt"; // 默认为左Alt
                    break;
            }

            options.Append($"--shortcut-mod={modifierStr} ");

            if (!string.IsNullOrEmpty(deviceSerial))
            {
                options.Append($"--serial {deviceSerial} ");
                DeviceSerial = deviceSerial;
            }

            return await StartScrcpyWithOptionsAsync(options.ToString());
        }

        /// <summary>
        /// 启动Scrcpy并嵌入到指定父窗口中
        /// </summary>
        /// <param name="parentHandle">父窗口句柄</param>
        /// <param name="x">窗口在父窗口中的X坐标</param>
        /// <param name="y">窗口在父窗口中的Y坐标</param>
        /// <param name="width">窗口宽度</param>
        /// <param name="height">窗口高度</param>
        /// <param name="param">设备序列号（可选）</param>
        /// <returns>是否成功启动</returns>
        public async Task<bool> StartEmbeddedAsync(
            IntPtr parentHandle,
            int x, int y,
            int width, int height,
            string param = null)
        {
            if (parentHandle == IntPtr.Zero)
                throw new ArgumentException("父窗口句柄不能为空", nameof(parentHandle));

            var options = new StringBuilder();

            // 如果指定了参数，则添加参数
            if (!string.IsNullOrEmpty(param))
            {
                options.Append($"{param} ");
            }
            options.Append("--window-x 32767 --window-y 32767 --window-width 1 --window-height 1 ");

            //// 自定义窗口样式 (子窗口)
            //int windowStyle = WindowHelper.WS_VISIBLE | WindowHelper.WS_CHILD ;

            return await StartScrcpyWithOptionsAsync(
                options.ToString(),
                parentHandle,
                null,
                null,
                x, y, width, height
            );
        }

        /// <summary>
        /// 获取当前Scrcpy窗口句柄
        /// </summary>
        /// <returns>窗口句柄</returns>
        public async Task<IntPtr> GetScrcpyWindowHandleAsync()
        {
            if (_scrcpyProcess == null || _scrcpyProcess.HasExited)
                return IntPtr.Zero;

            return await _scrcpyProcess.FindMainWindowAsync();
        }


        /// <summary>
        /// 设置当前Scrcpy窗口大小和位置
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <returns>是否成功</returns>
        public async Task<bool> SetWindowPosAndSizeAsync(int x, int y, int width, int height)
        {
            var handle = await GetScrcpyWindowHandleAsync();
            if (handle == IntPtr.Zero)
                return false;

            return WindowHelper.MoveAndResizeWindow(handle, x, y, width, height);
        }

        /// <summary>
        /// 设置当前Scrcpy窗口为置顶状态
        /// </summary>
        /// <param name="topMost">是否置顶</param>
        /// <returns>是否成功</returns>
        public async Task<bool> SetWindowTopMostAsync(bool topMost)
        {
            var handle = await GetScrcpyWindowHandleAsync();
            if (handle == IntPtr.Zero)
                return false;

            return WindowHelper.SetWindowTopMost(handle, topMost);
        }

        /// <summary>
        /// 设置当前Scrcpy窗口标题
        /// </summary>
        /// <param name="title">新标题</param>
        /// <returns>是否成功</returns>
        public async Task<bool> SetWindowTitleAsync(string title)
        {
            var handle = await GetScrcpyWindowHandleAsync();
            if (handle == IntPtr.Zero)
                return false;

            return WindowHelper.SetWindowTitle(handle, title);
        }

        #endregion

        #region 文件管理

        /// <summary>
        /// 安装APK到设备
        /// </summary>
        /// <param name="apkFilePath">APK文件路径</param>
        /// <returns>安装结果</returns>
        public async Task<string> InstallApkAsync(string apkFilePath)
        {
            if (!File.Exists(apkFilePath))
                throw new FileNotFoundException("APK文件不存在", apkFilePath);

            var deviceArg = string.IsNullOrEmpty(DeviceSerial) ? "" : $"-s {DeviceSerial} ";
            return await ExecuteAdbCommandAsync($"{deviceArg}install \"{apkFilePath}\"");
        }

        /// <summary>
        /// 将文件推送到设备
        /// </summary>
        /// <param name="localFilePath">本地文件路径</param>
        /// <param name="remotePath">远程目标路径（默认为/sdcard/Download/）</param>
        /// <returns>推送结果</returns>
        public async Task<string> PushFileAsync(string localFilePath, string remotePath = "/sdcard/Download/")
        {
            if (!File.Exists(localFilePath))
                throw new FileNotFoundException("本地文件不存在", localFilePath);

            // 确保远程路径以/结尾（表示目录）
            if (!remotePath.EndsWith("/"))
            {
                remotePath += "/";
            }

            var deviceArg = string.IsNullOrEmpty(DeviceSerial) ? "" : $"-s {DeviceSerial} ";
            return await ExecuteAdbCommandAsync($"{deviceArg}push \"{localFilePath}\" \"{remotePath}\"");
        }

        /// <summary>
        /// 从设备拉取文件
        /// </summary>
        /// <param name="remoteFilePath">远程文件路径</param>
        /// <param name="localPath">本地保存路径</param>
        /// <returns>拉取结果</returns>
        public async Task<string> PullFileAsync(string remoteFilePath, string localPath)
        {
            var deviceArg = string.IsNullOrEmpty(DeviceSerial) ? "" : $"-s {DeviceSerial} ";
            return await ExecuteAdbCommandAsync($"{deviceArg}pull \"{remoteFilePath}\" \"{localPath}\"");
        }

        #endregion

        #region 设备控制
        public class ScrcpyAppInfo
        {
            /// <summary>
            /// Application name
            /// </summary>
            public string AppName { get; set; }

            /// <summary>
            /// Application package name
            /// </summary>
            public string PackageName { get; set; }

            /// <summary>
            /// Application type (* for system apps, - for user apps, etc.)
            /// </summary>
            public string Type { get; set; }

            public override string ToString()
            {
                return $"{AppName} ({PackageName}) [{Type}]";
            }
        }
        /// <summary>
        /// 获取已安装的应用列表
        /// </summary>
        /// <returns>List of application information</returns>
        public async Task<List<ScrcpyAppInfo>> GetApplicationsAsync()
        {
            List<ScrcpyAppInfo> appList = new List<ScrcpyAppInfo>();

            string output = await ExecuteScrcpyCommandAsync("--list-apps");

            // Process the output line by line
            using (StringReader reader = new StringReader(output))
            {
                string line;
                bool startedAppList = false;

                while ((line = reader.ReadLine()) != null)
                {
                    // Start processing after this line
                    if (line.Contains("[server] INFO: List of apps:"))
                    {
                        startedAppList = true;
                        continue;
                    }

                    if (!startedAppList || string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    // Check if we've reached the end of the app list
                    if (line.StartsWith("C:") || line.StartsWith("/"))
                    {
                        break;
                    }

                    // Parse app entry line: " * AppName                        com.package.name"
                    Match match = Regex.Match(line, @"^\s*([* -])\s+(.*?)\s+(\S+)$");
                    if (match.Success)
                    {
                        string type = match.Groups[1].Value.Trim();
                        string appName = match.Groups[2].Value.Trim();
                        string packageName = match.Groups[3].Value.Trim();

                        appList.Add(new ScrcpyAppInfo
                        {
                            Type = type,
                            AppName = appName,
                            PackageName = packageName
                        });
                    }
                }
            }

            return appList;
        }
        /// <summary>
        /// 发送按键事件到设备
        /// </summary>
        /// <param name="keycode">按键代码</param>
        /// <returns>执行结果</returns>
        public async Task<string> SendKeyEventAsync(string keycode)
        {
            var deviceArg = string.IsNullOrEmpty(DeviceSerial) ? "" : $"-s {DeviceSerial} ";
            return await ExecuteAdbCommandAsync($"{deviceArg}shell input keyevent {keycode}");
        }

        /// <summary>
        /// 发送文本到设备
        /// </summary>
        /// <param name="text">文本内容</param>
        /// <returns>执行结果</returns>
        public async Task<string> SendTextAsync(string text)
        {
            var deviceArg = string.IsNullOrEmpty(DeviceSerial) ? "" : $"-s {DeviceSerial} ";
            return await ExecuteAdbCommandAsync($"{deviceArg}shell input text \"{text.Replace(" ", "%s").Replace("\"", "\\\"").Replace("'", "\\'")}\"");
        }

        /// <summary>
        /// 点击设备屏幕
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <returns>执行结果</returns>
        public async Task<string> TapScreenAsync(int x, int y)
        {
            var deviceArg = string.IsNullOrEmpty(DeviceSerial) ? "" : $"-s {DeviceSerial} ";
            return await ExecuteAdbCommandAsync($"{deviceArg}shell input tap {x} {y}");
        }

        /// <summary>
        /// 在设备屏幕上滑动
        /// </summary>
        /// <param name="x1">起始X坐标</param>
        /// <param name="y1">起始Y坐标</param>
        /// <param name="x2">结束X坐标</param>
        /// <param name="y2">结束Y坐标</param>
        /// <param name="duration">持续时间(毫秒)</param>
        /// <returns>执行结果</returns>
        public async Task<string> SwipeScreenAsync(int x1, int y1, int x2, int y2, int duration = 300)
        {
            var deviceArg = string.IsNullOrEmpty(DeviceSerial) ? "" : $"-s {DeviceSerial} ";
            return await ExecuteAdbCommandAsync($"{deviceArg}shell input swipe {x1} {y1} {x2} {y2} {duration}");
        }

        /// <summary>
        /// 获取设备屏幕分辨率
        /// </summary>
        /// <returns>屏幕分辨率(格式：宽x高)</returns>
        public async Task<string> GetScreenResolutionAsync()
        {
            var deviceArg = string.IsNullOrEmpty(DeviceSerial) ? "" : $"-s {DeviceSerial} ";
            var result = await ExecuteAdbCommandAsync($"{deviceArg}shell dumpsys window displays|grep init=");

            var match = ExtractSecondLineCur(result);


            return match;
        }
        static string ExtractSecondLineCur(string input)
        {
            var lines = input.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return null;

            var lastLine = lines[lines.Length - 1];

            var match = Regex.Match(lastLine, @"cur=(\d+x\d+)");
            return match.Success ? match.Value.Replace("cur=", "") : null;
        }
        /// <summary>
        /// 关闭设备屏幕
        /// </summary>
        /// <returns>执行结果</returns>
        public async Task<string> TurnScreenOffAsync()
        {
            return await SendKeyEventAsync("26"); // KEYCODE_POWER
        }

        /// <summary>
        /// 打开设备屏幕
        /// </summary>
        /// <returns>执行结果</returns>
        public async Task<string> TurnScreenOnAsync()
        {
            var deviceArg = string.IsNullOrEmpty(DeviceSerial) ? "" : $"-s {DeviceSerial} ";
            await ExecuteAdbCommandAsync($"{deviceArg}shell input keyevent 224"); // KEYCODE_WAKEUP
            return await ExecuteAdbCommandAsync($"{deviceArg}shell input keyevent 82"); // KEYCODE_MENU (解锁屏幕)
        }

        /// <summary>
        /// 设备截图并保存到本地
        /// </summary>
        /// <param name="outputPath">本地保存路径</param>
        /// <returns>执行结果</returns>
        public async Task<string> ScreenshotAsync(string outputPath)
        {
            var deviceArg = string.IsNullOrEmpty(DeviceSerial) ? "" : $"-s {DeviceSerial} ";
            var tempFile = "/sdcard/screenshot.png";

            // 截图保存到设备
            await ExecuteAdbCommandAsync($"{deviceArg}shell screencap -p {tempFile}");

            // 拉取到本地
            var result = await ExecuteAdbCommandAsync($"{deviceArg}pull {tempFile} \"{outputPath}\"");

            // 删除设备上的临时文件
            await ExecuteAdbCommandAsync($"{deviceArg}shell rm {tempFile}");

            return result;
        }

        /// <summary>
        /// 增大设备音量
        /// </summary>
        /// <returns>执行结果</returns>
        public async Task<string> VolumeUpAsync()
        {
            return await SendKeyEventAsync("24"); // KEYCODE_VOLUME_UP
        }

        /// <summary>
        /// 降低设备音量
        /// </summary>
        /// <returns>执行结果</returns>
        public async Task<string> VolumeDownAsync()
        {
            return await SendKeyEventAsync("25"); // KEYCODE_VOLUME_DOWN
        }
        /// <summary>
        /// 点击电源键
        /// </summary>
        /// <returns>执行结果</returns>
        public async Task<string> PressPowerButtonAsync()
        {
            return await SendKeyEventAsync("26"); // KEYCODE_POWER
        }

        /// <summary>
        /// 关闭设备屏幕（保持镜像）- 通过向Scrcpy窗口发送Alt+O组合键
        /// </summary>
        /// <returns>执行结果</returns>
        public async Task<string> TurnScreenOffKeepMirroringAsync()
        {
            // 检查窗口句柄是否有效
            if (ScrcpyWindow == IntPtr.Zero)
            {
                MessageBox.Show("无效的Scrcpy窗口句柄");
            }

            // 确保窗口处于前台
            SetForegroundWindow(ScrcpyWindow);
            await Task.Delay(100); // 等待窗口激活

            // 模拟Alt+O按键
            // 按下Alt键
            var inputs = new INPUT[4];

            // 按下Alt键
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = VK_MENU;
            inputs[0].U.ki.dwFlags = 0;

            // 按下O键
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = 48; // O键的虚拟键码
            inputs[1].U.ki.dwFlags = 0;

            // 释放O键
            inputs[2].type = INPUT_KEYBOARD;
            inputs[2].U.ki.wVk = 48;
            inputs[2].U.ki.dwFlags = KEYEVENTF_KEYUP;

            // 释放Alt键
            inputs[3].type = INPUT_KEYBOARD;
            inputs[3].U.ki.wVk = VK_MENU;
            inputs[3].U.ki.dwFlags = KEYEVENTF_KEYUP;

            // 发送按键事件
            SendInput(4, inputs, Marshal.SizeOf(typeof(INPUT)));

            return "已模拟Alt+O组合键发送到Scrcpy窗口";
        }
        #region Windows API声明
        // 常量定义
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const int VK_MENU = 0x12;  // Alt键

        // 结构体定义
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public int type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        #endregion

        /// <summary>
        /// 切换横屏竖屏
        /// </summary>
        /// <returns>执行结果</returns>
        public async Task<string> ToggleScreenOrientationAsync()
        {
            var deviceArg = string.IsNullOrEmpty(DeviceSerial) ? "" : $"-s {DeviceSerial} ";

            // 先获取当前方向
            var currentOrientation = await ExecuteAdbCommandAsync($"{deviceArg}shell settings get system accelerometer_rotation");

            if (currentOrientation.Trim() == "0")
            {
                // 当前是固定方向，获取当前具体方向
                var userRotation = await ExecuteAdbCommandAsync($"{deviceArg}shell settings get system user_rotation");
                int rotation = int.TryParse(userRotation.Trim(), out int r) ? r : 0;

                // 切换方向（0=竖屏，1=横屏，2=反向竖屏，3=反向横屏）
                int newRotation = (rotation == 0) ? 1 : 0;
                return await ExecuteAdbCommandAsync($"{deviceArg}shell settings put system user_rotation {newRotation}");
            }
            else
            {
                // 当前是自动旋转，先关闭自动旋转然后设置为横屏或竖屏
                await ExecuteAdbCommandAsync($"{deviceArg}shell settings put system accelerometer_rotation 0");
                // 设置为横屏
                return await ExecuteAdbCommandAsync($"{deviceArg}shell settings put system user_rotation 1");
            }
        }
        //获取当前方向
        public async Task<string> GetCurrentOrientationAsync()
        {
            //adb shell "dumpsys input | grep SurfaceOrientation"
            var deviceArg = string.IsNullOrEmpty(DeviceSerial) ? "" : $"-s {DeviceSerial} ";
            return await ExecuteAdbCommandAsync($"{deviceArg}shell settings get system user_rotation");
        }

        /// <summary>
        /// 展开通知面板
        /// </summary>
        /// <returns>执行结果</returns>
        public async Task<string> ExpandNotificationPanelAsync()
        {
            var deviceArg = string.IsNullOrEmpty(DeviceSerial) ? "" : $"-s {DeviceSerial} ";
            return await ExecuteAdbCommandAsync($"{deviceArg}shell cmd statusbar expand-notifications");
        }
        /// <summary>
        /// 显示最近任务列表
        /// </summary>
        /// <returns>执行结果</returns>
        public async Task<string> ShowRecentTasksAsync()
        {
            return await SendKeyEventAsync("187"); // KEYCODE_APP_SWITCH
        }

        /// <summary>
        /// 按下Home键
        /// </summary>
        /// <returns>执行结果</returns>
        public async Task<string> PressHomeAsync()
        {
            return await SendKeyEventAsync("3"); // KEYCODE_HOME
        }

        /// <summary>
        /// 按下返回键
        /// </summary>
        /// <returns>执行结果</returns>
        public async Task<string> PressBackAsync()
        {
            return await SendKeyEventAsync("4"); // KEYCODE_BACK
        }




        #endregion

        #region 辅助方法

        /// <summary>
        /// 执行ADB命令
        /// </summary>
        /// <param name="command">ADB命令参数</param>
        /// <returns>命令输出</returns>
        private async Task<string> ExecuteAdbCommandAsync(string command)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = AdbPath,
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var output = new StringBuilder();
                var error = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        output.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        error.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0 && error.Length > 0)
                {
                    OnErrorReceived?.Invoke(this, error.ToString());
                }

                return output.ToString();
            }
        }

        /// <summary>
        /// 执行Scrcpy命令
        /// </summary>
        /// <param name="arguments">命令参数</param>
        /// <returns>命令输出</returns>
        private async Task<string> ExecuteScrcpyCommandAsync(string arguments)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = ScrcpyPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,  // Set UTF-8 encoding
                    StandardErrorEncoding = Encoding.UTF8    // Set UTF-8 encoding
                };

                // 设置环境变量
                if (!string.IsNullOrEmpty(AdbPath))
                {
                    process.StartInfo.EnvironmentVariables["ADB"] = AdbPath;
                }

                if (!string.IsNullOrEmpty(ScrcpyServerPath))
                {
                    process.StartInfo.EnvironmentVariables["SCRCPY_SERVER_PATH"] = ScrcpyServerPath;
                }

                var output = new StringBuilder();
                var error = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        output.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        error.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                string result = output.ToString();

                if (process.ExitCode != 0 && error.Length > 0)
                {
                    OnErrorReceived?.Invoke(this, error.ToString());
                    result += error.ToString();
                }

                return result;
            }
        }

        #endregion

        #region 事件

        /// <summary>
        /// 日志接收事件
        /// </summary>
        public event EventHandler<string> OnLogReceived;

        /// <summary>
        /// 错误接收事件
        /// </summary>
        public event EventHandler<string> OnErrorReceived;

        /// <summary>
        /// 进程退出事件
        /// </summary>
        public event EventHandler OnProcessExited;

        /// <summary>
        /// 窗口初始化事件
        /// </summary>
        public event EventHandler<IntPtr> OnWindowInitialized;

        #endregion

        #region IDisposable实现

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    public static class WindowHelper
    {
        #region Windows API 常量定义

        // 窗口样式
        public const int WS_BORDER = 0x800000;
        public const int WS_CAPTION = 0xC00000;
        public const int WS_SIZEBOX = 0x00040000;

        public const int WS_CHILD = 0x40000000;
        public const int WS_CLIPCHILDREN = 0x2000000;
        public const int WS_CLIPSIBLINGS = 0x4000000;
        public const int WS_DISABLED = 0x8000000;
        public const int WS_DLGFRAME = 0x400000;
        public const int WS_GROUP = 0x20000;
        public const int WS_HSCROLL = 0x100000;
        public const int WS_MAXIMIZE = 0x1000000;
        public const int WS_MAXIMIZEBOX = 0x10000;
        public const int WS_MINIMIZE = 0x20000000;
        public const int WS_MINIMIZEBOX = 0x20000;
        public const int WS_OVERLAPPED = 0x0;
        public const int WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
        public const int WS_POPUP = unchecked((int)0x80000000);
        public const int WS_POPUPWINDOW = WS_POPUP | WS_BORDER | WS_SYSMENU;
        public const int WS_SYSMENU = 0x80000;
        public const int WS_TABSTOP = 0x10000;
        public const int WS_THICKFRAME = 0x40000;
        public const int WS_VISIBLE = 0x10000000;
        public const int WS_VSCROLL = 0x200000;

        // 扩展窗口样式
        public const int WS_EX_ACCEPTFILES = 0x00000010;
        public const int WS_EX_APPWINDOW = 0x00040000;
        public const int WS_EX_CLIENTEDGE = 0x00000200;
        public const int WS_EX_COMPOSITED = 0x02000000;
        public const int WS_EX_CONTEXTHELP = 0x00000400;
        public const int WS_EX_CONTROLPARENT = 0x00010000;
        public const int WS_EX_DLGMODALFRAME = 0x00000001;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_LAYOUTRTL = 0x00400000;
        public const int WS_EX_LEFT = 0x00000000;
        public const int WS_EX_LEFTSCROLLBAR = 0x00004000;
        public const int WS_EX_LTRREADING = 0x00000000;
        public const int WS_EX_MDICHILD = 0x00000040;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_NOINHERITLAYOUT = 0x00100000;
        public const int WS_EX_NOPARENTNOTIFY = 0x00000004;
        public const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;
        public const int WS_EX_OVERLAPPEDWINDOW = WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE;
        public const int WS_EX_PALETTEWINDOW = WS_EX_WINDOWEDGE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
        public const int WS_EX_RIGHT = 0x00001000;
        public const int WS_EX_RIGHTSCROLLBAR = 0x00000000;
        public const int WS_EX_RTLREADING = 0x00002000;
        public const int WS_EX_STATICEDGE = 0x00020000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_TOPMOST = 0x00000008;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_WINDOWEDGE = 0x00000100;

        // 窗口消息
        public const int WM_SETTEXT = 0x000C;
        public const int SWP_NOACTIVATE = 0x0010;
        public const int SWP_NOMOVE = 0x0002;
        public const int SWP_NOSIZE = 0x0001;
        public const int SWP_NOZORDER = 0x0004;
        public const int SWP_SHOWWINDOW = 0x0040;
        public const int SWP_ASYNCWINDOWPOS = 0x4000;
        public const int HWND_TOP = 0;
        public const int HWND_BOTTOM = 1;
        public const int HWND_TOPMOST = -1;
        public const int HWND_NOTOPMOST = -2;
        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;

        #endregion

        #region Windows API 函数声明

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetDesktopWindow();

        // 委托定义
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        #endregion

        #region 窗口查找方法

        /// <summary>
        /// 查找指定进程的主窗口
        /// </summary>
        /// <param name="process">目标进程</param>
        /// <returns>窗口句柄</returns>
        public static IntPtr FindMainWindow(this Process process)
        {
            if (process == null || process.HasExited)
                return IntPtr.Zero;

            IntPtr mainWindowHandle = IntPtr.Zero;
            uint processId = (uint)process.Id;

            EnumWindows((hWnd, lParam) =>
            {
                // 检查窗口是否属于目标进程
                GetWindowThreadProcessId(hWnd, out uint windowProcessId);
                if (windowProcessId == processId && IsWindowVisible(hWnd))
                {
                    // 获取窗口标题
                    var title = new StringBuilder(256);
                    GetWindowText(hWnd, title, title.Capacity);

                    // 如果窗口有标题，则认为是主窗口
                    if (title.Length > 0)
                    {
                        mainWindowHandle = hWnd;
                        return false; // 停止枚举
                    }
                }
                return true; // 继续枚举
            }, IntPtr.Zero);

            return mainWindowHandle;
        }
        public static void SetBorderlessWindow(IntPtr hWnd)
        {
            // 获取当前窗口样式
            int style = GetWindowLong(hWnd, GWL_STYLE);

            // 移除边框相关样式
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);

            // 设置新的窗口样式
            SetWindowLong(hWnd, GWL_STYLE, style);
        }
        /// <summary>
        /// 异步等待并查找进程的主窗口
        /// </summary>
        /// <param name="process">目标进程</param>
        /// <param name="timeout">超时时间(毫秒)</param>
        /// <param name="retryInterval">重试间隔(毫秒)</param>
        /// <returns>窗口句柄</returns>
        public static async Task<IntPtr> FindMainWindowAsync(this Process process, int timeout = 5000, int retryInterval = 100)
        {
            if (process == null || process.HasExited)
                return IntPtr.Zero;

            // 尝试直接获取主窗口句柄
            IntPtr handle = process.MainWindowHandle;
            if (handle != IntPtr.Zero)
                return handle;

            int elapsedTime = 0;
            while (elapsedTime < timeout)
            {
                // 当进程启动时，窗口可能需要一些时间才能创建，所以我们需要等待
                handle = process.FindMainWindow();
                if (handle != IntPtr.Zero)
                    return handle;

                await Task.Delay(retryInterval);
                elapsedTime += retryInterval;

                // 检查进程是否还在运行
                if (process.HasExited)
                    return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// 通过窗口标题查找窗口
        /// </summary>
        /// <param name="windowTitle">窗口标题</param>
        /// <returns>窗口句柄</returns>
        public static IntPtr FindWindowByTitle(string windowTitle)
        {
            if (string.IsNullOrEmpty(windowTitle))
                return IntPtr.Zero;

            IntPtr foundWindow = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    var title = new StringBuilder(256);
                    GetWindowText(hWnd, title, title.Capacity);

                    if (title.ToString().Contains(windowTitle))
                    {
                        foundWindow = hWnd;
                        return false; // 停止枚举
                    }
                }
                return true; // 继续枚举
            }, IntPtr.Zero);

            return foundWindow;
        }

        #endregion

        #region 窗口样式设置方法

        /// <summary>
        /// 设置窗口的父窗口
        /// </summary>
        /// <param name="childWindow">子窗口句柄</param>
        /// <param name="parentWindow">父窗口句柄</param>
        /// <returns>原父窗口句柄</returns>
        public static IntPtr SetWindowParent(IntPtr childWindow, IntPtr parentWindow)
        {
            if (childWindow == IntPtr.Zero)
                return IntPtr.Zero;

            // 如果父窗口为零，则设置为桌面窗口
            if (parentWindow == IntPtr.Zero)
                parentWindow = GetDesktopWindow();

            return SetParent(childWindow, parentWindow);
        }

        /// <summary>
        /// 设置窗口样式
        /// </summary>
        /// <param name="window">窗口句柄</param>
        /// <param name="newStyle">新的窗口样式</param>
        /// <returns>旧的窗口样式</returns>
        public static int SetWindowStyle(IntPtr window, int newStyle)
        {
            if (window == IntPtr.Zero)
                return 0;

            int oldStyle = GetWindowLong(window, GWL_STYLE);
            SetWindowLong(window, GWL_STYLE, newStyle);
            return oldStyle;
        }

        /// <summary>
        /// 设置窗口扩展样式
        /// </summary>
        /// <param name="window">窗口句柄</param>
        /// <param name="newExStyle">新的扩展窗口样式</param>
        /// <returns>旧的扩展窗口样式</returns>
        public static int SetWindowExStyle(IntPtr window, int newExStyle)
        {
            if (window == IntPtr.Zero)
                return 0;

            int oldExStyle = GetWindowLong(window, GWL_EXSTYLE);
            SetWindowLong(window, GWL_EXSTYLE, newExStyle);
            return oldExStyle;
        }

        /// <summary>
        /// 移动窗口并调整大小
        /// </summary>
        /// <param name="window">窗口句柄</param>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="repaint">是否重绘</param>
        /// <returns>是否成功</returns>
        public static bool MoveAndResizeWindow(IntPtr window, int x, int y, int width, int height, bool repaint = true)
        {
            if (window == IntPtr.Zero)
                return false;

            return MoveWindow(window, x, y, width, height, repaint);
        }

        /// <summary>
        /// 设置窗口置顶状态
        /// </summary>
        /// <param name="window">窗口句柄</param>
        /// <param name="topMost">是否置顶</param>
        /// <returns>是否成功</returns>
        public static bool SetWindowTopMost(IntPtr window, bool topMost)
        {
            if (window == IntPtr.Zero)
                return false;

            IntPtr hWndInsertAfter = topMost ? new IntPtr(HWND_TOPMOST) : new IntPtr(HWND_NOTOPMOST);
            return SetWindowPos(window, hWndInsertAfter, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        }

        /// <summary>
        /// 设置窗口标题
        /// </summary>
        /// <param name="window">窗口句柄</param>
        /// <param name="title">新标题</param>
        /// <returns>是否成功</returns>
        public static bool SetWindowTitle(IntPtr window, string title)
        {
            if (window == IntPtr.Zero)
                return false;

            return SendMessage(window, WM_SETTEXT, IntPtr.Zero, title) != 0;
        }

        #endregion
    }
    public static class ProcessExtensions
    {
        /// <summary>
        /// 异步等待进程退出
        /// </summary>
        /// <param name="process">进程对象</param>
        /// <returns>表示异步操作的任务</returns>
        public static Task WaitForExitAsync(this Process process)
        {
            return WaitForExitAsync(process, CancellationToken.None);
        }

        /// <summary>
        /// 异步等待进程退出，支持取消
        /// </summary>
        /// <param name="process">进程对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        public static Task WaitForExitAsync(this Process process, CancellationToken cancellationToken)
        {
            if (process == null)
                throw new ArgumentNullException(nameof(process));

            var tcs = new TaskCompletionSource<object>();

            // 如果进程已经退出，立即完成任务
            if (process.HasExited)
            {
                tcs.TrySetResult(null);
                return tcs.Task;
            }

            // 注册进程退出事件
            void ProcessExited(object sender, EventArgs e)
            {
                process.Exited -= ProcessExited;
                tcs.TrySetResult(null);
            }

            // 注册取消令牌
            if (cancellationToken != CancellationToken.None)
            {
                cancellationToken.Register(() =>
                {
                    process.Exited -= ProcessExited;
                    tcs.TrySetCanceled();
                });
            }

            // 确保启用事件的引发
            process.EnableRaisingEvents = true;
            process.Exited += ProcessExited;

            // 再次检查进程是否已退出（避免竞争条件）
            if (process.HasExited)
            {
                process.Exited -= ProcessExited;
                tcs.TrySetResult(null);
            }

            return tcs.Task;
        }

        /// <summary>
        /// 异步等待进程退出，并设置超时
        /// </summary>
        /// <param name="process">进程对象</param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <returns>如果进程在超时前退出则返回true，否则返回false</returns>
        public static async Task<bool> WaitForExitAsync(this Process process, int timeout)
        {
            if (process == null)
                throw new ArgumentNullException(nameof(process));

            if (process.HasExited)
                return true;

            var cancellationTokenSource = new CancellationTokenSource(timeout);
            try
            {
                await WaitForExitAsync(process, cancellationTokenSource.Token);
                return true;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            finally
            {
                cancellationTokenSource.Dispose();
            }
        }
    }
}