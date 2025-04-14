using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using 搞机助手Ex.Helper;
using 搞机助手Ex.MyControl;
using static 搞机助手Ex.Helper.ADBClient;

namespace 搞机助手Ex.Windows
{
    /// <summary>
    /// Windows_Freeze_App.xaml 的交互逻辑
    /// </summary>
    public partial class Windows_Freeze_App : Window
    {
        public Windows_Freeze_App(int type = 0)//0:冻结应用  1：启动虚拟桌面APP
        {
            InitializeComponent();
            switch(type)
            {
                case 0:
                    textblock_title.Text = "冻结应用";
                    冻结Panel.Visibility = Visibility.Visible;
                    this.Loaded += Window_Loaded_冻结应用;
                    break;
                case 1:
                    textblock_title.Text = "虚拟桌面";
                    TabItem_冻结应用.Header = "三方应用";
                    TabItem_解冻应用.Header = "系统应用";
                    this.Loaded += Window_Loaded_虚拟桌面;
                    break;
                default:
                    textblock_title.Text = "冻结应用";
                    冻结Panel.Visibility = Visibility.Visible;
                    this.Loaded += Window_Loaded_冻结应用;
                    break;
            }

        }
        #region 窗体控制

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

                this.Close();

        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {

                this.WindowState = WindowState.Minimized;
        }

       

        // 切换到另一个主题
        private void Themes_Click(object sender, RoutedEventArgs e)
        {

            App.CurrentTheme = App.CurrentTheme == App.ThemeType.Dark
                ? App.ThemeType.Light
                : App.ThemeType.Dark;
        }

        #endregion

        private async void Window_Loaded_虚拟桌面(object sender, RoutedEventArgs e)
        {
            try
            {
               ScrcpyHelper scrcpyHelper = new ScrcpyHelper();
                var scrcpyAppInfos = await scrcpyHelper.GetApplicationsAsync();
                foreach (var appInfo in scrcpyAppInfos)
                {
                    AppInfo appInfo1 = new AppInfo
                    {
                        IconUrl = null,
                        AppName = appInfo.AppName,
                        PackageName = appInfo.PackageName
                    };
                    // 创建 APPInfoButton 实例
                    var button = new APPInfoButton(appInfo1)
                    {
                        Margin = new Thickness(5)
                    };
                    button.Click += (ns, nargs) =>
                    {
                        //2880x1620
                        Windows_Scrcpy scrcpy = new Windows_Scrcpy($"--no-vd-system-decorations --new-display=1920x1080/192 --start-app={appInfo1.PackageName}");
                        scrcpy.Show();
                        this.Close();
                    };
                    
                       
                        // 在UI线程上创建和添加按钮
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (appInfo.Type == "-")//三方
                            {
                                Stackpanel_冻结应用.Children.Add(button);
                            }
                           
                            else
                            {
                                Stackpanel_解冻应用.Children.Add(button);
                            }
                        });
                    
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载应用时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void Window_Loaded_冻结应用(object sender, RoutedEventArgs e)
        {
            try
            {
                // 调用adb查询所有三方|未经冻结的包名
                var packs = await MainWindow.BackgroundThread._adbClient.GetAllPackageNamesAsync(PackageFilterType.ThirdParty | PackageFilterType.Unfrozen);
                // 调用adb查询所有三方|已经冻结的包名
                var frozenPacks = await MainWindow.BackgroundThread._adbClient.GetAllPackageNamesAsync(PackageFilterType.ThirdParty | PackageFilterType.Frozen);

                Dispatcher.Invoke(() => {
                    TabItem_冻结应用.Header = $"冻结应用 ({packs.Count})";
                    TabItem_解冻应用.Header = $"已冻结应用 ({frozenPacks.Count})";
                });

                int totalPacks = packs.Count + frozenPacks.Count;
                int processedCount = 0;

                // 获取 CPU 核心数
                int maxDegreeOfParallelism = Environment.ProcessorCount;

                using (var semaphore = new SemaphoreSlim(maxDegreeOfParallelism / 2))
                {
                    // 处理未冻结应用
                    var tasks1 = packs.Select(async pack =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            var appInfo = await AppInfoFetcher.GetAppInfoAsync(pack);

                            // 在UI线程上创建和添加按钮
                            await Dispatcher.InvokeAsync(() =>
                            {
                                // 创建 APPInfoButton 实例
                                var button = new APPInfoButton(appInfo)
                                {
                                    Margin = new Thickness(5)
                                };

                                // 设置按钮的点击事件
                                button.Click += async (s, args) =>
                                {
                                    // 弹窗确认是否冻结
                                    MessageBoxResult result = MessageBox.Show("是否冻结？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                                    if (result != MessageBoxResult.Yes)
                                    {
                                        return;
                                    }

                                    // 移除按钮后再执行冻结操作
                                    Stackpanel_冻结应用.Children.Remove(button);

                                    // 执行冻结操作
                                    await MainWindow.BackgroundThread._adbClient.FreezeAppAsync(appInfo.PackageName);

                                    // 创建新按钮添加到已冻结列表
                                    var newButton = new APPInfoButton(appInfo)
                                    {
                                        Margin = new Thickness(5)
                                    };

                                    newButton.Click += async (ns, nargs) =>
                                    {
                                        MessageBoxResult nresult = MessageBox.Show("是否解冻？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                                        if (nresult != MessageBoxResult.Yes)
                                        {
                                            return;
                                        }

                                        Stackpanel_解冻应用.Children.Remove(newButton);

                                        await MainWindow.BackgroundThread._adbClient.UnfreezeAppAsync(appInfo.PackageName);

                                        // 重新加载列表更新状态
                                        await RefreshAppLists();
                                    };

                                    Stackpanel_解冻应用.Children.Add(newButton);

                                    // 更新两者数量标题
                                    TabItem_冻结应用.Header = $"冻结应用 ({Stackpanel_冻结应用.Children.Count})";
                                    TabItem_解冻应用.Header = $"解冻应用 ({Stackpanel_解冻应用.Children.Count})";
                                };

                                // 将按钮添加到布局中
                                Stackpanel_冻结应用.Children.Add(button);
                            });
                        }
                        finally
                        {
                            semaphore.Release();
                            // 更新进度
                            Interlocked.Increment(ref processedCount);
                            await Dispatcher.InvokeAsync(() =>
                            {
                                textblock_title.Text = $"加载中... {processedCount}/{totalPacks}";
                            });
                        }
                    });

                    // 处理已冻结应用
                    var tasks2 = frozenPacks.Select(async pack =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            var appInfo = await AppInfoFetcher.GetAppInfoAsync(pack);

                            // 在UI线程上创建和添加按钮
                            await Dispatcher.InvokeAsync(() =>
                            {
                                // 创建 APPInfoButton 实例
                                var button = new APPInfoButton(appInfo)
                                {
                                    Margin = new Thickness(5)
                                };

                                // 设置按钮的点击事件
                                button.Click += async (s, args) =>
                                {
                                    // 弹窗确认是否解冻
                                    MessageBoxResult result = MessageBox.Show("是否解冻？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                                    if (result != MessageBoxResult.Yes)
                                    {
                                        return;
                                    }

                                    // 移除按钮后再执行解冻操作
                                    Stackpanel_解冻应用.Children.Remove(button);

                                    // 执行解冻操作
                                    await MainWindow.BackgroundThread._adbClient.UnfreezeAppAsync(appInfo.PackageName);

                                    // 创建新按钮添加到未冻结列表
                                    var newButton = new APPInfoButton(appInfo)
                                    {
                                        Margin = new Thickness(5)
                                    };

                                    newButton.Click += async (ns, nargs) =>
                                    {
                                        MessageBoxResult nresult = MessageBox.Show("是否冻结？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                                        if (nresult != MessageBoxResult.Yes)
                                        {
                                            return;
                                        }

                                        Stackpanel_冻结应用.Children.Remove(newButton);

                                        await MainWindow.BackgroundThread._adbClient.FreezeAppAsync(appInfo.PackageName);

                                        // 重新加载列表更新状态
                                        //await RefreshAppLists();
                                    };

                                    Stackpanel_冻结应用.Children.Add(newButton);

                                    // 更新两者数量标题
                                    TabItem_冻结应用.Header = $"冻结应用 ({Stackpanel_冻结应用.Children.Count})";
                                    TabItem_解冻应用.Header = $"解冻应用 ({Stackpanel_解冻应用.Children.Count})";
                                };

                                // 将按钮添加到布局中
                                Stackpanel_解冻应用.Children.Add(button);
                            });
                        }
                        finally
                        {
                            semaphore.Release();
                            // 更新进度
                            Interlocked.Increment(ref processedCount);
                            await Dispatcher.InvokeAsync(() =>
                            {
                                textblock_title.Text = $"加载中... {processedCount}/{totalPacks}";
                            });
                        }
                    });

                    // 并行等待所有任务完成
                    await Task.WhenAll(tasks1.Concat(tasks2));
                }

                // 加载完成后更新标题
                await Dispatcher.InvokeAsync(() => {
                    textblock_title.Text = "冻结应用";
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载应用时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 添加一个辅助方法来刷新应用列表
        private async Task RefreshAppLists()
        {
            try
            {
                // 不清空当前列表，获取所有的应用包名，进行更新，有的不动，没有的就删除，多的就添加
                // 调用adb查询所有三方|未经冻结的包名
                var packs = await MainWindow.BackgroundThread._adbClient.GetAllPackageNamesAsync(PackageFilterType.ThirdParty | PackageFilterType.Unfrozen);
                // 调用adb查询所有三方|已经冻结的包名
                var frozenPacks = await MainWindow.BackgroundThread._adbClient.GetAllPackageNamesAsync(PackageFilterType.ThirdParty | PackageFilterType.Frozen);
                //查看Stackpanel_冻结应用中有没有比获取到的包名多的，删除多的
                foreach (var child in Stackpanel_冻结应用.Children.OfType<APPInfoButton>().ToList())
                {
                    if (!packs.Contains(child.appInfo.PackageName))
                    {
                        Stackpanel_冻结应用.Children.Remove(child);
                    }
                }
                //查看Stackpanel_解冻应用中有没有比获取到的包名多的，删除多的
                foreach (var child in Stackpanel_解冻应用.Children.OfType<APPInfoButton>().ToList())
                {
                    if (!frozenPacks.Contains(child.appInfo.PackageName))
                    {
                        Stackpanel_解冻应用.Children.Remove(child);
                    }
                }


                foreach (var pack in packs)
                {
                    // 检查是否已经存在
                    var existingButton = Stackpanel_冻结应用.Children.OfType<APPInfoButton>().FirstOrDefault(b => b.appInfo.PackageName == pack);
                    if (existingButton == null)
                    {
                        // 如果不存在，则添加
                        var appInfo = new AppInfo
                        {
                            PackageName = pack,
                            AppName = "未知应用",
                            IconUrl = null

                        };

                        var button = new APPInfoButton(appInfo)
                        {
                            Margin = new Thickness(5)
                        };
                        button.Click += async (ns, nargs) =>
                        {
                            MessageBoxResult nresult = MessageBox.Show($"是否冻结{appInfo.PackageName}？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (nresult != MessageBoxResult.Yes)
                            {
                                return;
                            }

                            Stackpanel_冻结应用.Children.Remove(button);

                            await MainWindow.BackgroundThread._adbClient.FreezeAppAsync(appInfo.PackageName);

                            // 重新加载列表更新状态
                            await RefreshAppLists();
                        };
                        Stackpanel_冻结应用.Children.Add(button);
                    }
                }
                // 更新已冻结应用列表
                foreach (var pack in frozenPacks)
                {
                    // 检查是否已经存在
                    var existingButton = Stackpanel_解冻应用.Children.OfType<APPInfoButton>().FirstOrDefault(b => b.appInfo.PackageName == pack);
                    if (existingButton == null)
                    {
                        // 如果不存在，则添加
                        var appInfo = new AppInfo
                        {
                            PackageName = pack,
                            AppName = "未知应用",
                            IconUrl = null

                        };
                        var button = new APPInfoButton(appInfo)
                        {
                            Margin = new Thickness(5)
                        };
                        button.Click += async (ns, nargs) =>
                        {
                            MessageBoxResult nresult = MessageBox.Show($"是否解冻{appInfo.PackageName}？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (nresult != MessageBoxResult.Yes)
                            {
                                return;
                            }

                            Stackpanel_解冻应用.Children.Remove(button);

                            await MainWindow.BackgroundThread._adbClient.UnfreezeAppAsync(appInfo.PackageName);

                            // 重新加载列表更新状态
                            await RefreshAppLists();
                        };
                        Stackpanel_解冻应用.Children.Add(button);
                    }
                }

                //更新标签页标题
                TabItem_冻结应用.Header = $"冻结应用 ({Stackpanel_冻结应用.Children.Count})";
                TabItem_解冻应用.Header = $"解冻应用 ({Stackpanel_解冻应用.Children.Count})";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新应用列表时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // 创建一个临时列表存储需要移除的未知应用
            var itemsToRemove = new List<APPInfoButton>();

            // 处理 Stackpanel_冻结应用
            foreach (var child in Stackpanel_冻结应用.Children.OfType<APPInfoButton>())
            {
                if (child.Text == "未知应用")
                {

                    itemsToRemove.Add(child); // 标记为待移除
                }
            }
            // 从 Stackpanel_冻结应用 移除
            foreach (var item in itemsToRemove)
            {
                Stackpanel_冻结应用.Children.Remove(item);
            }

            // 清空临时列表
            itemsToRemove.Clear();

            // 处理 Stackpanel_解冻应用
            foreach (var child in Stackpanel_解冻应用.Children.OfType<APPInfoButton>())
            {
                if (child.Text == "未知应用")
                {
                    itemsToRemove.Add(child); // 标记为待移除
                }
            }
            // 从 Stackpanel_解冻应用 移除
            foreach (var item in itemsToRemove)
            {
                Stackpanel_解冻应用.Children.Remove(item);
            }
            //更新标签页标题
            TabItem_冻结应用.Header = $"冻结应用 ({Stackpanel_冻结应用.Children.Count})";
            TabItem_解冻应用.Header = $"解冻应用 ({Stackpanel_解冻应用.Children.Count})";
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            _ = RefreshAppLists();
        }
    }
}
