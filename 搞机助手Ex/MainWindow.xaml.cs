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
        static BackgroundThread BackgroundThread;
        // 定义页面实例
        private Device_Information deviceInformationPage;
        private SystemMode systemModePage = new SystemMode();
        private RecoveryMode recoveryModePage = new RecoveryMode();
        private FastbootMode fastbootModePage = new FastbootMode();
        private Expansion expansionPage = new Expansion();


        public MainWindow()
        {
            InitializeComponent();
            //打开后台线程
            BackgroundThread = new BackgroundThread(textblock_title);
            deviceInformationPage = new Device_Information(BackgroundThread);
            InitViews();
            


        }
        // 初始化菜单
        private void InitViews()
        {
            MainFram.Children.Clear();
            var v = new Frame();
            MainFram.Children.Add(v);
            v.Navigate(deviceInformationPage);

            btnDeviceInfo.Checked += (s, args) =>
            {
                MainFram.Children.Clear();
                var frame = new Frame();
                MainFram.Children.Add(frame);
                frame.Navigate(deviceInformationPage);
            };

            btnSystemMode.Checked += (s, args) =>
            {
                MainFram.Children.Clear();
                var frame = new Frame();
                MainFram.Children.Add(frame);
                frame.Navigate(systemModePage);
            };

            btnRecoveryMode.Checked += (s, args) =>
            {
                MainFram.Children.Clear();
                var frame = new Frame();
                MainFram.Children.Add(frame);
                frame.Navigate(recoveryModePage);
            };

            btnBootMode.Checked += (s, args) =>
            {
                MainFram.Children.Clear();
                var frame = new Frame();
                MainFram.Children.Add(frame);
                frame.Navigate(fastbootModePage);
            };

            btnExtendedFunc.Checked += (s, args) =>
            {
                MainFram.Children.Clear();
                var frame = new Frame();
                MainFram.Children.Add(frame);
                frame.Navigate(expansionPage);
            };
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


    }
}
