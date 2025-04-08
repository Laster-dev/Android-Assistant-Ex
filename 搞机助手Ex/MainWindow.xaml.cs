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
using System.Windows.Navigation;
using System.Windows.Shapes;
using 搞机助手Ex.Helper;
using 搞机助手Ex.Views;

namespace 搞机助手Ex
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        //后台扫描
        public static BackgroundThread BackgroundThread;
        // 定义页面实例
        private Device_Information deviceInformationPage;
        private SystemMode systemModePage = new SystemMode();
        private RecoveryMode recoveryModePage = new RecoveryMode();
        private FastbootMode fastbootModePage = new FastbootMode();
        private Expansion expansionPage = new Expansion();


        public MainWindow()
        {
            InitializeComponent();
            InitViews();
            HideBoundingBox(this);
        }
        /// <summary>
        /// 初始化导航视图，设置页面导航和事件处理
        /// </summary>
        private void InitViews()
        { 
            //打开后台线程
            BackgroundThread = new BackgroundThread(textblock_title);
            deviceInformationPage = new Device_Information(BackgroundThread);
            // 初始导航到设备信息页面
            NavigateToPage(deviceInformationPage);

            // 使用字典存储按钮与对应页面的映射关系
            var navigationMap = new Dictionary<RadioButton, Page>
    {
        { btnDeviceInfo, deviceInformationPage },
        { btnSystemMode, systemModePage },
        { btnRecoveryMode, recoveryModePage },
        { btnBootMode, fastbootModePage },
        { btnExtendedFunc, expansionPage }
    };

            // 为每个导航按钮注册统一的事件处理逻辑
            foreach (var entry in navigationMap)
            {
                var button = entry.Key;
                var page = entry.Value;

                button.Checked += (sender, args) =>
                {
                    NavigateToPage(page);
                };
            }
        }

        /// <summary>
        /// 导航到指定页面，清空并重建框架以避免导航条
        /// </summary>
        /// <param name="page">要导航到的页面</param>
        private void NavigateToPage(Page page)
        {
            // 清空当前内容区域
            MainFram.Children.Clear();

            // 创建新的框架并禁用导航界面
            var frame = new Frame
            {
                NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Hidden
            };

            // 添加框架到内容区域
            MainFram.Children.Add(frame);

            // 导航到指定页面
            frame.Navigate(page);

            HideBoundingBox(page);
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
            var storyboard = (Storyboard)this.Resources["MinimizeStoryboard"];
            storyboard.Completed += (s, a) => //动画完成后执行
            {
                this.Close();
            };
            storyboard.Begin();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            var storyboard = (Storyboard)this.Resources["MinimizeStoryboard"];
            storyboard.Completed += (s, a) => //动画完成后执行
            {
                this.WindowState = WindowState.Minimized;
            };

            storyboard.Begin();
        }

        //窗口创建时
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.Opacity = 1;
            //居中
            this.Left = (SystemParameters.WorkArea.Width - this.Width) / 2;
            this.Top = (SystemParameters.WorkArea.Height - this.Height) / 2;

            //this.WindowState = WindowState.Normal;
            var storyboard = (Storyboard)this.Resources["RestartStoryboard"];
            storyboard.Begin();
        }

        //窗口状态改变时
        protected override void OnStateChanged(EventArgs e)
        {
            var storyboard = (Storyboard)this.Resources["RestartStoryboard"];
            storyboard.Begin();
        }

        // 切换到另一个主题
        private void Themes_Click(object sender, RoutedEventArgs e)
        {
           
            App.CurrentTheme = App.CurrentTheme == App.ThemeType.Dark
                ? App.ThemeType.Light
                : App.ThemeType.Dark;
        }

        #endregion


        /// <summary>
        /// 隐藏控件获得焦点时的虚线框
        /// </summary>
        /// <param name="root"></param>
        public static void HideBoundingBox(object root)
        {
            Control control = root as Control;
            if (control != null)
            {
                control.FocusVisualStyle = null;
            }

            if (root is DependencyObject)
            {
                foreach (object child in LogicalTreeHelper.GetChildren((DependencyObject)root))
                {
                    HideBoundingBox(child);
                }
            }
        }

    }
}
