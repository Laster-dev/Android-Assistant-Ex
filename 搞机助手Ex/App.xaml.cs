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
        private static ThemeType? _currentTheme;  // 使用可空类型，表示初始未设置状态

        /// <summary>
        /// 获取当前主题并设置新主题
        /// </summary>
        public static ThemeType CurrentTheme
        {
            get
            {
                // 如果主题未初始化，则获取系统主题
                if (!_currentTheme.HasValue)
                {
                    _currentTheme = GetSystemTheme();
                }
                return _currentTheme.Value;
            }
            set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    _ = SwitchTheme(value);
                }
            }
        }

        /// <summary>
        /// 获取系统当前的主题设置
        /// </summary>
        private static ThemeType GetSystemTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object appsUseLightTheme = key.GetValue("AppsUseLightTheme");
                        if (appsUseLightTheme != null)
                        {
                            // 0 表示深色主题，1 表示浅色主题
                            return (int)appsUseLightTheme == 0 ? ThemeType.Dark : ThemeType.Light;
                        }
                    }
                }

                // 如果无法读取注册表，则检查系统颜色
                if (SystemParameters.WindowGlassColor.R < 128 &&
                    SystemParameters.WindowGlassColor.G < 128 &&
                    SystemParameters.WindowGlassColor.B < 128)
                {
                    return ThemeType.Dark;
                }

                return ThemeType.Light;
            }
            catch
            {
                // 如果发生任何错误，默认返回浅色主题
                return ThemeType.Light;
            }
        }


        public static async Task SwitchTheme(ThemeType theme)
        {
            try
            {
                var window = Application.Current.MainWindow;
                var content = window.Content as UIElement;
                if (content == null) return;

                // 保存当前内容的快照
                var currentContent = content;

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
                Console.WriteLine($"切换主题时出错: {ex.Message}\n\n{ex.StackTrace}");
                //MessageBox.Show($"切换主题时出错: {ex.Message}\n\n{ex.StackTrace}", "主题切换错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);

                //删除Tools\\文件夹
                //如果文件夹存在
                if (Directory.Exists("Tools"))
                {
                    Directory.Delete("Tools", true);
                }
                File.WriteAllBytes("Tools.zip", Resource1.Tools);

                ZipFile.ExtractToDirectory("Tools.zip", "Tools\\");

                File.Delete("Tools.zip");

                // 初始化主题 - 只需访问 CurrentTheme 属性，它会自动初始化为系统主题
                ThemeType initialTheme = CurrentTheme;

                // 第一次加载主题资源
                _ = SwitchTheme(initialTheme);

                // 监听系统主题变化
                SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            }
            catch { }
           
        }



        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ThemeType newTheme = GetSystemTheme();
                    if (_currentTheme != newTheme)
                    {
                        CurrentTheme = newTheme;
                    }
                });
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            base.OnExit(e);
        }
    }
}
