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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using 搞机助手Ex.Helper;

namespace 搞机助手Ex.Views
{
    /// <summary>
    /// Device_Information.xaml 的交互逻辑
    /// </summary>
    public partial class Device_Information : Page
    {
        BackgroundThread _backgroundThread;
        public Device_Information(BackgroundThread backgroundThread)
        {
            InitializeComponent();
            _backgroundThread = backgroundThread;
            //创建一个时钟，每隔5秒检测一次设备
            Timer timer = new Timer(CheckDevice, null, 0, 5000);
        }

        private async void CheckDevice(object state)
        {

            var devices = await DeviceInfo.GetDeviceInfoAsync(_backgroundThread._adbClient);

            // 使用 Dispatcher 在 UI 线程中更新 UI 元素
            Application.Current.Dispatcher.Invoke(() =>
            {
                TextBlock_Manufacturer.Text = "制造厂商：" + devices.Manufacturer.ToString();
                TextBlock_DeviceModel.Text = "设备型号：" + devices.Model.ToString();
                TextBlock_Androidversion.Text = "安卓版本：" + devices.AndroidVersion.ToString();
                TextBlock_DeviceCode.Text = "设备代号：" + devices.DeviceCodeName.ToString();
                TextBlock_SN.Text = "SN："+ devices.SerialNumber.ToString();
                TextBlock_CPUCode.Text = "CPU:" + devices.CpuArchitecture;
                TextBolck_Status.Text = "设备状态：" + devices.CurrentModeString.ToString();
            });
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("请前往设置页面进行设置！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            // Disable the button to prevent multiple clicks
            var button = (Button)sender;
            button.IsEnabled = false;

            try
            {
                // Add user feedback
                var originalContent = button.Content;
                button.Content = "正在重启..."; // "Rebooting..." in Chinese

                // Check if ADB client is initialized
                if (_backgroundThread == null || _backgroundThread._adbClient == null)
                {
                    MessageBox.Show("ADB客户端未初始化，请检查应用配置。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Execute the reboot command with a timeout
                var result = await _backgroundThread._adbClient.RebootAsync();

            }
            catch (Exception ex)
            {
                // Show the error message
                MessageBox.Show($"发生错误: {ex.Message}", "异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable the button
                button.IsEnabled = true;
                button.Content = "重启设备"; // Reset button text (assuming it was "Reboot Device")
            }
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            //调用ADB命令，重启设备到Recovery模式
            _ = await _backgroundThread._adbClient.RebootAsync(BootState.Recovery);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            //调用ADB命令，重启设备到Bootloader模式
            _ = _backgroundThread._adbClient.RebootAsync(BootState.Bootloader);
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            //调用ADB命令，关机
            _ = _backgroundThread._adbClient.RebootAsync(BootState.Poweroff);
        }
    }
}
