using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace 搞机助手Ex.Windows
{
    /// <summary>
    /// Windows_Process_Management.xaml 的交互逻辑
    /// </summary>
    public partial class Windows_Process_Management : Window
    {
        public Windows_Process_Management()
        {
            InitializeComponent();
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
