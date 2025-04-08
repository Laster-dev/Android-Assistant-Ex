using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using 搞机助手Ex.Views;

namespace 搞机助手Ex
{
    public partial class App : Application
    {
        private string _toolsPath;
        private string _zipPath;
        public enum ThemeType
        {
            Dark,
            Light
        }

        /// <summary>
        /// 当前主题
        /// </summary>
        private static ThemeType _currentTheme = ThemeType.Dark;

        /// <summary>
        /// 获取当前主题并设置新主题
        /// </summary>
        public static ThemeType CurrentTheme
        {
            get
            {
                return _currentTheme;
            }
            set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    // 修复：在UI线程上正确处理异步操作
                    Application.Current.Dispatcher.InvokeAsync(() =>
                        SwitchThemeAsync(value));
                }
            }
        }

        // 异步切换主题方法
        public static async Task SwitchThemeAsync(ThemeType theme)
        {
            try
            {
                var window = Application.Current.MainWindow;
                if (window == null) return;

                var content = window.Content as UIElement;
                if (content == null) return;

                // 所有操作已经在UI线程上，无需额外的Dispatcher调用
                // 创建淡出动画
                var fadeOutAnimation = new DoubleAnimation
                {
                    From = 1,
                    To = 0.3,
                    Duration = TimeSpan.FromMilliseconds(100)
                };

                // 使用简单的延迟而不是复杂的TaskCompletionSource
                content.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
                await Task.Delay(120); // 确保动画有足够时间完成

                // 切换主题资源
                var mergedDicts = Application.Current.Resources.MergedDictionaries;

                // 移除现有主题资源
                for (int i = mergedDicts.Count - 1; i >= 0; i--)
                {
                    var dict = mergedDicts[i];
                    string source = dict.Source?.ToString() ?? "";
                    if (source.Contains("DarkTheme.xaml") || source.Contains("LightTheme.xaml"))
                    {
                        mergedDicts.RemoveAt(i);
                    }
                }

                // 添加新的主题字典
                var newThemeUri = new Uri(
                    theme == ThemeType.Dark
                    ? "pack://application:,,,/Themes/DarkTheme.xaml"
                    : "pack://application:,,,/Themes/LightTheme.xaml",
                    UriKind.Absolute);

                var newThemeDict = new ResourceDictionary() { Source = newThemeUri };
                mergedDicts.Add(newThemeDict);

                // 创建淡入动画
                var fadeInAnimation = new DoubleAnimation
                {
                    From = 0.3,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(100)
                };

                // 应用淡入动画
                content.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);

                // 添加诊断信息
                Console.WriteLine($"Theme switched to: {theme}");

              
            }
            catch (Exception ex)
            {
                MessageBox.Show($"切换主题时出错: {ex.Message}", "主题切换错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // 设置全局未处理异常处理器
                this.DispatcherUnhandledException += App_DispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

                _toolsPath = Path.Combine(Directory.GetCurrentDirectory(), "Tools");
                _zipPath = Path.Combine(Directory.GetCurrentDirectory(), "Tools.zip");
                if (!Directory.Exists("Tools"))
                {
                    File.WriteAllBytes("Tools.zip", Resource1.Tools);

                    ZipFile.ExtractToDirectory("Tools.zip", "Tools\\");

                    File.Delete("Tools.zip");
                }
                // 先应用初始主题，再调用base.OnStartup
                ApplyInitialTheme();

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动时发生错误: {ex.Message}", "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        private void ApplyInitialTheme()
        {
            try
            {
                var mergedDicts = Application.Current.Resources.MergedDictionaries;

                // 添加基础主题字典
                var themeUri = new Uri("pack://application:,,,/Themes/LightTheme.xaml", UriKind.Absolute);
                var themeDict = new ResourceDictionary() { Source = themeUri };
                mergedDicts.Add(themeDict);

                _currentTheme = ThemeType.Light;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用初始主题时出错: {ex.Message}", "主题错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"未处理的UI线程异常: {e.Exception.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // 防止应用崩溃
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show($"未处理的应用域异常: {ex.Message}", "严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            MessageBox.Show($"未观察到的任务异常: {e.Exception.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.SetObserved(); // 防止崩溃
        }
    }
}