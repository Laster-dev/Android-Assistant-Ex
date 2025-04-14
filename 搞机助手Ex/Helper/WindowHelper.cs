using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace 搞机助手Ex.Helper
{
    /// <summary>
    /// 窗口操作辅助类
    /// </summary>
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
}