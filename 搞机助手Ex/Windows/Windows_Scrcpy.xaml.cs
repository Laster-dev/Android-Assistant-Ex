using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using 搞机助手Ex.Helper;

namespace 搞机助手Ex.Windows
{
    /// <summary>
    /// Scrcpy窗口的交互逻辑
    /// </summary>
    public partial class Windows_Scrcpy : Window
    {
        #region Fields

        private ScrcpyHelper _scrcpyHelper;
        private System.Windows.Forms.Panel _hostPanel;
        private double _dpiScale = 1.0;
        private string _param = string.Empty;
        #endregion

        #region Native Methods

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern bool MoveWindow(IntPtr hwnd, int x, int y, int cx, int cy, bool repaint);

        #endregion

        #region Constructor

        public Windows_Scrcpy(string param = null)
        {
            InitializeComponent();
            _param = param;
            //// 设置最小尺寸
            //MinWidth = 225 + 34;
            //MinHeight = 400 + 34;

            // 设置应用程序为 DPI 感知
            SetProcessDPIAware();
            
            // 初始化ScrcpyHelper
            InitializeScrcpyHelper();
            
            // 注册窗口关闭事件
            Closed += Windows_Scrcpy_Closed;
            
            // 缓存DPI值以避免频繁查询
            UpdateDpiScale();
        }

        #endregion

        #region Initialization Methods

        /// <summary>
        /// 初始化Scrcpy助手
        /// </summary>
        private void InitializeScrcpyHelper()
        {
            //结束所有Scrcpy进程
            var scrcpyProcesses = Process.GetProcessesByName("scrcpy");
            foreach (var process in scrcpyProcesses)
            {
                try
                {
                    process.Kill();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"结束Scrcpy进程失败: {ex.Message}");
                }
            }
            try
            {
                string scrcpyPath = FindScrcpyPath();
                if (string.IsNullOrEmpty(scrcpyPath))
                {
                    ShowError("未能找到scrcpy可执行文件，请确保已正确安装scrcpy。");
                    return;
                }

                // 获取adb路径（通常在scrcpy同目录）
                string adbPath = Path.Combine(Path.GetDirectoryName(scrcpyPath), "adb.exe");

                // 创建ScrcpyHelper实例
                _scrcpyHelper = new ScrcpyHelper(scrcpyPath, adbPath);

                // 注册事件处理
                RegisterScrcpyEvents();
            }
            catch (Exception ex)
            {
                ShowError($"初始化Scrcpy助手失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 注册Scrcpy事件监听
        /// </summary>
        private void RegisterScrcpyEvents()
        {
            if (_scrcpyHelper == null) return;

            _scrcpyHelper.OnLogReceived += (sender, log) => Debug.WriteLine($"Scrcpy Log: {log}");
            _scrcpyHelper.OnErrorReceived += (sender, error) => Debug.WriteLine($"Scrcpy Error: {error}");
            _scrcpyHelper.OnProcessExited += (sender, args) => Dispatcher.Invoke(HandleScrcpyExited);
        }

        /// <summary>
        /// 查找Scrcpy可执行文件路径
        /// </summary>
        private string FindScrcpyPath()
        {
            // 尝试在常见位置查找scrcpy.exe
            string[] possiblePaths = new[]
            {
                @"Tools\scrcpy.exe", // 相对于应用程序目录
            };

            // 首先检查相对路径
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            foreach (var path in possiblePaths)
            {
                string fullPath = Path.IsPathRooted(path) ? path : Path.Combine(baseDir, path);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 窗口加载完成事件
        /// </summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_scrcpyHelper == null)
            {
                ShowError("Scrcpy助手初始化失败，无法启动镜像。");
                return;
            }

            try
            {
                // 初始化WindowsFormsHost
                InitializeHostPanel();

                // 启动Scrcpy
                await StartScrcpyAsync();
            }
            catch (Exception ex)
            {
                ShowError($"启动Scrcpy镜像时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 窗口大小改变事件
        /// </summary>
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                // 更新WindowsFormsHost尺寸
                WindowsFormsHost1.Height = maingrid.ActualHeight;
                WindowsFormsHost1.Width = maingrid.ActualWidth;
                
                // 调整Scrcpy窗口大小
                AdjustScrcpyWindowSize();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"处理窗口大小变化出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        private void Windows_Scrcpy_Closed(object sender, EventArgs e)
        {
            // 释放资源
            _scrcpyHelper?.Dispose();
            _scrcpyHelper = null;
        }

        /// <summary>
        /// 处理DPI变化
        /// </summary>
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);

            // 只有在DPI实际变化时才更新
            if (Math.Abs(_dpiScale - newDpi.DpiScaleX) > 0.01)
            {
                _dpiScale = newDpi.DpiScaleX;
                AdjustScrcpyWindowSize();
            }
        }

        #endregion

        #region UI Control Event Handlers

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 最小化按钮点击事件
        /// </summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 切换主题按钮点击事件
        /// </summary>
        private void Themes_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentTheme = App.CurrentTheme == App.ThemeType.Dark
                ? App.ThemeType.Light
                : App.ThemeType.Dark;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 初始化WindowsFormsHost面板
        /// </summary>
        private void InitializeHostPanel()
        {
            if (WindowsFormsHost1 != null)
            {
                _hostPanel = new System.Windows.Forms.Panel
                {
                    BackColor = System.Drawing.Color.Black
                };
                WindowsFormsHost1.Child = _hostPanel;
            }
        }

        /// <summary>
        /// 启动Scrcpy镜像
        /// </summary>
        private async Task StartScrcpyAsync()
        {
            // 更新DPI缩放
            UpdateDpiScale();

            // 计算嵌入区域尺寸
            int width = (int)(WindowsFormsHost1.Width * _dpiScale);
            int height = (int)(WindowsFormsHost1.Height * _dpiScale);

            // 启动嵌入式Scrcpy
            bool success = await _scrcpyHelper.StartEmbeddedAsync(
                _hostPanel.Handle,
                0, 0, width, height,
                _param
            );

            if (!success)
            {
                ShowError("启动Scrcpy镜像失败，请检查设备连接状态或Scrcpy配置。");
            }
            else if (_scrcpyHelper.ScrcpyWindow == IntPtr.Zero)
            {
                // 获取窗口句柄（如果OnWindowInitialized事件未触发）
                _scrcpyHelper.ScrcpyWindow = await _scrcpyHelper.GetScrcpyWindowHandleAsync();
            }
        }

        /// <summary>
        /// 根据DPI调整窗口大小
        /// </summary>
        private void AdjustScrcpyWindowSize()
        {
            if (_scrcpyHelper?._scrcpyProcess == null ||
                _scrcpyHelper.ScrcpyWindow == IntPtr.Zero) 
                return;

            // 计算考虑 DPI 的尺寸
            int width = (int)(WindowsFormsHost1.Width * _dpiScale);
            int height = (int)(WindowsFormsHost1.Height * _dpiScale);

            MoveWindow(_scrcpyHelper.ScrcpyWindow, 0, 0, width, height, true);
        }

        /// <summary>
        /// 处理Scrcpy进程退出
        /// </summary>
        private void HandleScrcpyExited()
        {
            _scrcpyHelper.ScrcpyWindow = IntPtr.Zero;
            Debug.WriteLine("Scrcpy进程已退出");
        }

        /// <summary>
        /// 更新DPI缩放值
        /// </summary>
        private void UpdateDpiScale()
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                Matrix m = source.CompositionTarget.TransformToDevice;
                _dpiScale = m.M11;
            }
        }

        #endregion

        #region Message Methods

        /// <summary>
        /// 显示错误信息
        /// </summary>
        private void ShowError(string message)
        {
            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// 显示提示信息
        /// </summary>
        private void ShowInfo(string message)
        {
            MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left; // 最左坐标
            public int Top; // 最上坐标
            public int Right; // 最右坐标
            public int Bottom; // 最下坐标
        }
        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            
            var ScreenResolution = await _scrcpyHelper.GetScreenResolutionAsync();
            //MessageBox.Show(ScreenResolution);
            string[] resolutionParts = ScreenResolution.Split('x');
            string width = "",height = "";
            
            if (resolutionParts.Length == 2)
            {
                width = resolutionParts[0].Trim();
                height = resolutionParts[1].Trim();
               
            }
            else
            {
                MessageBox.Show("无法获取屏幕分辨率。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Point sizeInDips = DpiHelper.ConvertPixelsToDips(int.Parse(width), int.Parse(height));

            // 竖屏
            this.Width = sizeInDips.X / 2.5 + 47.15;
            this.Height = sizeInDips.Y / 2.5 + 70;



        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            _ = _scrcpyHelper.VolumeUpAsync();
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            _ = _scrcpyHelper.VolumeDownAsync();
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            _ = _scrcpyHelper.ShowRecentTasksAsync();
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            _ = _scrcpyHelper.PressHomeAsync();
        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            _ = _scrcpyHelper.PressBackAsync();
        }

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            //this.Width = sizeInDips.X / 2.5 + 47.15;
            //this.Height = sizeInDips.Y / 2.5 + 70;

            //宽高交换
            double temp = this.Width - 47.15;
            this.Width = this.Height - 70 + 47.15;
            this.Height = temp + 70;
            

        }

        private void Button_Click_8(object sender, RoutedEventArgs e)
        {
            _ = _scrcpyHelper.ExpandNotificationPanelAsync();
        }

        private void Button_Click_9(object sender, RoutedEventArgs e)
        {
            _ = _scrcpyHelper.TurnScreenOffKeepMirroringAsync();
        }

        private void Button_Click_10(object sender, RoutedEventArgs e)
        {
            _ = _scrcpyHelper.TurnScreenOffAsync();
        }
        //PressBackAsync
    }
    public static class DpiHelper
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

        [DllImport("gdi32.dll")]
        static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        private const int LOGPIXELSX = 88;
        private const int LOGPIXELSY = 90;

        // 从物理像素转换为WPF的设备无关单位
        public static Point ConvertPixelsToDips(int pixelX, int pixelY)
        {
            Matrix transformToDevice = GetTransformToDevice();
            return new Point(pixelX / transformToDevice.M11, pixelY / transformToDevice.M22);
        }

        // 从WPF的设备无关单位转换为物理像素
        public static Point ConvertDipsToPixels(double dipsX, double dipsY)
        {
            Matrix transformToDevice = GetTransformToDevice();
            return new Point(dipsX * transformToDevice.M11, dipsY * transformToDevice.M22);
        }

        private static Matrix GetTransformToDevice()
        {
            var source = PresentationSource.FromVisual(Application.Current.MainWindow);
            if (source != null)
            {
                return source.CompositionTarget.TransformToDevice;
            }
            else
            {
                // 如果无法获取当前窗口的DPI，则使用系统DPI
                return GetSystemDpiMatrix();
            }
        }

        private static Matrix GetSystemDpiMatrix()
        {
            IntPtr desktop = GetDC(IntPtr.Zero);
            int dpiX = GetDeviceCaps(desktop, LOGPIXELSX);
            int dpiY = GetDeviceCaps(desktop, LOGPIXELSY);
            ReleaseDC(IntPtr.Zero, desktop);

            return new Matrix(dpiX / 96.0, 0, 0, dpiY / 96.0, 0, 0);
        }
    }
}