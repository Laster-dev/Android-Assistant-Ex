using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace wpftest
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // 设置应用程序为 DPI 感知
            SetProcessDPIAware();
            WindowsFormsHost1.Height = Height;
            WindowsFormsHost1.Width = Width;
        }

        // Win32 API 声明
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hwnd, int x, int y, int cx, int cy, bool repaint);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern int GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        // 获取和设置窗口样式的API
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        // 窗口样式常量
        const int GWL_STYLE = -16;
        const int WS_CAPTION = 0x00C00000;
        const int WS_THICKFRAME = 0x00040000;
        const int WS_SYSMENU = 0x00080000;
        const int WS_MINIMIZEBOX = 0x00020000;
        const int WS_MAXIMIZEBOX = 0x00010000;

        Process _process;
        System.Windows.Forms.Panel _hostPanel;
        private double _dpiScale = 1.0; // DPI 缩放值

        private void UpdateDpiScale()
        {
            // 获取当前的 DPI 值
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                Matrix m = source.CompositionTarget.TransformToDevice;
                _dpiScale = m.M11; // 水平和垂直缩放通常是一样的
            }
        }

        public bool StartAndEmbedProcess(string processPath)
        {
            bool isStartAndEmbedSuccess = false;

            if (WindowsFormsHost1 != null)
            {
                _hostPanel = new System.Windows.Forms.Panel();
                WindowsFormsHost1.Child = _hostPanel;
            }

            _process = System.Diagnostics.Process.Start(processPath);

            // 确保可获取到句柄
            Thread thread = new Thread(new ThreadStart(() =>
            {
                while (true)
                {
                    if (_process.MainWindowHandle != (IntPtr)0)
                    {
                        break;
                    }
                    Thread.Sleep(10);
                }
            }));
            thread.Start();
            MessageBox.Show("");
           // Thread.Sleep(1000);
            isStartAndEmbedSuccess = EmbedApp(_process);
            if (!isStartAndEmbedSuccess)
            {
                CloseApp(_process);
            }
            return isStartAndEmbedSuccess;
        }

        public bool EmbedExistProcess(Process process)
        {
            _process = process;
            return EmbedApp(process);
        }

        /// <summary>
        /// 设置窗口为无边框
        /// </summary>
        private void SetBorderlessWindow(IntPtr hWnd)
        {
            // 获取当前窗口样式
            int style = GetWindowLong(hWnd, GWL_STYLE);

            // 移除边框相关样式
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);

            // 设置新的窗口样式
            SetWindowLong(hWnd, GWL_STYLE, style);
        }

        /// <summary>
        /// 将外进程嵌入到当前程序
        /// </summary>
        /// <param name="process"></param>
        private bool EmbedApp(Process process)
        {
            // 是否嵌入成功标志，用作返回值
            bool isEmbedSuccess = false;
            // 外进程句柄
            IntPtr processHwnd = process.MainWindowHandle;
            // 容器句柄
            IntPtr panelHwnd = _hostPanel.Handle;

            if (processHwnd != (IntPtr)0 && panelHwnd != (IntPtr)0)
            {
                // 设置为无边框窗口
                SetBorderlessWindow(processHwnd);

                // 把本窗口句柄与目标窗口句柄关联起来
                int setTime = 0;
                while (!isEmbedSuccess && setTime < 10)
                {
                    isEmbedSuccess = (SetParent(processHwnd, panelHwnd) != 0);
                    Thread.Sleep(100);
                    setTime++;
                }

                // 更新 DPI 缩放值
                UpdateDpiScale();
                // 设置初始尺寸和位置，考虑 DPI 缩放
                AdjustWindowSizeForDpi();
            }

            return isEmbedSuccess;
        }

        private void AdjustWindowSizeForDpi()
        {
            if (_process == null || _process.MainWindowHandle == IntPtr.Zero) return;

            // 计算考虑 DPI 的尺寸
            int width = (int)(WindowsFormsHost1.Width * _dpiScale);
            int height = (int)(WindowsFormsHost1.Height * _dpiScale);

            MoveWindow(_process.MainWindowHandle, 0, 0, width, height, true);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (_process != null && _process.MainWindowHandle != IntPtr.Zero)
            {
                // 更新 DPI 缩放值并调整窗口大小
                UpdateDpiScale();
                AdjustWindowSizeForDpi();
            }
            base.OnRender(drawingContext);
        }

        // 监听 DPI 变化（例如将窗口从一个显示器拖到另一个显示器）
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            _dpiScale = newDpi.DpiScaleX;
            AdjustWindowSizeForDpi();
        }

        /// <summary>
        /// 关闭进程
        /// </summary>
        /// <param name="process"></param>
        private void CloseApp(Process process)
        {
            if (process != null && !process.HasExited)
            {
                process.Kill();
            }
        }

        public void CloseProcess()
        {
            CloseApp(_process);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            StartAndEmbedProcess(@"C:\Users\28572\Desktop\scrcpy-win64-v3.2\scrcpy.exe");
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            WindowsFormsHost1.Height = Height;
            WindowsFormsHost1.Width = Width ;
        }
    }
}