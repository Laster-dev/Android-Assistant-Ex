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
        public enum ThemeType
        {
            Dark,
            Light
        }

        /// <summary>
        /// 当前主题
        /// </summary>
        private static ThemeType _currentTheme = ThemeType.Light;  // 不需要可空类型

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
                    // 使用正确的方式调用异步方法，确保不会阻塞UI线程
                    SwitchThemeAsync(value).ConfigureAwait(false);
                }
            }
        }

        // 重命名为Async来表明这是异步方法
        public static async Task SwitchThemeAsync(ThemeType theme)
        {
            try
            {
                var window = Application.Current.MainWindow;
                if (window == null) return;  // 检查窗口是否存在

                var content = window.Content as UIElement;
                if (content == null) return;

                // 在UI线程上执行动画
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        // 创建淡出动画
                        var fadeOutAnimation = new DoubleAnimation
                        {
                            From = 1,
                            To = 0.3,
                            Duration = TimeSpan.FromMilliseconds(100)
                        };

                        // 等待淡出动画完成
                        var tcs = new TaskCompletionSource<bool>();
                        fadeOutAnimation.Completed += (s, e) => tcs.SetResult(true);

                        // 应用淡出动画
                        content.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
                        await tcs.Task;

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
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"切换主题动画时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            }
            catch (Exception ex)
            {
                // 确保异常信息被显示而不是忽略
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

                // 应用初始化前加载主题
                ApplyInitialTheme();

                base.OnStartup(e);

            }
            catch (Exception ex)
            {
                // 不要忽略异常，至少记录或显示它们
                MessageBox.Show($"启动时发生错误: {ex.Message}", "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        private void ApplyInitialTheme()
        {
            try
            {
                // 从注册表或其他设置源确定初始主题
                // 为了简单起见，我们使用默认的Light主题

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