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
    public partial class Device_Information : Page, IDisposable
    {
        /// <summary>
        /// 后台线程对象，负责与ADB通信
        /// </summary>
        private readonly BackgroundThread _backgroundThread;

        /// <summary>
        /// 设备检测定时器，用于周期性检查设备状态
        /// </summary>
        private Timer _deviceCheckTimer;

        /// <summary>
        /// 标记当前页面是否已释放资源
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="backgroundThread">后台线程对象，用于执行ADB命令</param>
        public Device_Information(BackgroundThread backgroundThread)
        {
            InitializeComponent();
            _backgroundThread = backgroundThread ?? throw new ArgumentNullException(nameof(backgroundThread));

            // 创建一个定时器，每隔1秒检测一次设备状态
            _deviceCheckTimer = new Timer(CheckDevice, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }
        /// <summary>
        /// 检查设备状态的回调方法，由定时器触发
        /// </summary>
        /// <param name="state">定时器状态对象，此处未使用</param>
        private async void CheckDevice(object state)
        {
            try
            {
                // 检查ADB客户端是否初始化
                if (_backgroundThread._adbClient == null)
                {
                    await UpdateUIAsync(() =>
                    {
                        ClearDeviceInfo();
                        TextBolck_Status.Text = "设备状态：ADB 未初始化";
                    });
                    return;
                }

                // 异步获取设备信息
                var devices = await DeviceInfo.GetDeviceInfoAsync(_backgroundThread._adbClient);

                // 在UI线程上更新界面
                await UpdateUIAsync(() =>
                {
                    if (devices == null)
                    {
                        // 设备未连接或获取设备信息失败时清空显示
                        ClearDeviceInfo();
                        TextBolck_Status.Text = "设备状态：未连接";
                        return;
                    }

                    // 更新UI显示设备详细信息
                    TextBlock_Manufacturer.Text = $"制造厂商：{devices.Manufacturer ?? "未知"}";
                    TextBlock_DeviceModel.Text = $"设备型号：{devices.Model ?? "未知"}";
                    TextBlock_Androidversion.Text = $"安卓版本：{devices.AndroidVersion ?? "未知"}";
                    TextBlock_DeviceCode.Text = $"设备代号：{devices.DeviceCodeName ?? "未知"}";
                    TextBlock_SN.Text = $"SN：{devices.SerialNumber ?? "未知"}";
                    TextBlock_CPUCode.Text = $"CPU：{devices.CpuArchitecture ?? "未知"}";

                    // 获取设备模式的字符串表示
                    string modeString = GetDeviceModeDisplayString(devices.CurrentMode);
                    TextBolck_Status.Text = $"设备状态：{modeString}";

                    // 根据设备模式启用/禁用相应的控制面板
                    DisableAllPanels();

                    switch (devices.CurrentMode)
                    {
                        case DeviceMode.System:
                            StackPanel_System.IsEnabled = true;
                            break;
                        case DeviceMode.Recovery:
                            StackPanel_Recovery.IsEnabled = true;
                            break;
                        case DeviceMode.Fastboot:
                            StackPanel_Fastboot.IsEnabled = true;
                            break;
                            // 其他模式下都保持所有面板禁用
                    }
                });
            }
            catch (Exception ex)
            {
                // 捕获并处理检测过程中的异常
                await UpdateUIAsync(() =>
                {
                    ClearDeviceInfo();
                    TextBolck_Status.Text = $"设备状态：检测异常 - {ex.Message}";
                });
            }
        }

        /// <summary>
        /// 禁用所有控制面板
        /// </summary>
        private void DisableAllPanels()
        {
            StackPanel_System.IsEnabled = false;
            StackPanel_Recovery.IsEnabled = false;
            StackPanel_Fastboot.IsEnabled = false;
        }

        /// <summary>
        /// 获取设备模式的显示字符串
        /// </summary>
        /// <param name="mode">设备模式枚举值</param>
        /// <returns>对应的中文描述</returns>
        private string GetDeviceModeDisplayString(DeviceMode mode)
        {
            switch (mode)
            {
                case DeviceMode.System:
                    return "正常模式";
                case DeviceMode.Recovery:
                    return "Recovery模式";
                case DeviceMode.Fastboot:
                    return "Fastboot模式";
                case DeviceMode.NoDevice:
                    return "未连接";
                case DeviceMode.Download:
                    return "下载模式(奥丁模式)";
                default:
                    return "未知模式";
            }
        }

        /// <summary>
        /// 清空设备信息显示
        /// </summary>
        private void ClearDeviceInfo()
        {
            // 将所有设备信息相关文本框重置为"未知"状态
            TextBlock_Manufacturer.Text = "制造厂商：未知";
            TextBlock_DeviceModel.Text = "设备型号：未知";
            TextBlock_Androidversion.Text = "安卓版本：未知";
            TextBlock_DeviceCode.Text = "设备代号：未知";
            TextBlock_SN.Text = "SN：未知";
            TextBlock_CPUCode.Text = "CPU：未知";
        }

        /// <summary>
        /// 在UI线程上执行操作的辅助方法
        /// </summary>
        /// <param name="action">需要在UI线程上执行的操作</param>
        /// <returns>表示异步操作的任务</returns>
        private Task UpdateUIAsync(Action action)
        {
            // 检查应用程序和调度器是否可用
            if (Application.Current == null || Application.Current.Dispatcher == null)
                return Task.CompletedTask;

            // 在UI线程上执行指定操作
            return Application.Current.Dispatcher.InvokeAsync(action).Task;
        }

        /// <summary>
        /// 超链接点击事件处理
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("请前往设置页面进行设置！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 普通重启设备按钮点击事件处理
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteDeviceActionAsync(sender as Button, "重启设备", "正在重启...",
                async () => await _backgroundThread._adbClient.RebootAsync());
        }

        /// <summary>
        /// 重启到Recovery模式按钮点击事件处理
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            await ExecuteDeviceActionAsync(sender as Button, "Recovery 模式", "正在重启到 Recovery...",
                async () => await _backgroundThread._adbClient.RebootAsync(BootState.Recovery));
        }

        /// <summary>
        /// 重启到Bootloader模式按钮点击事件处理
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private async void Button_Click_2(object sender, RoutedEventArgs e)
        {
            await ExecuteDeviceActionAsync(sender as Button, "Bootloader 模式", "正在重启到 Bootloader...",
                async () => await _backgroundThread._adbClient.RebootAsync(BootState.Bootloader));
        }

        /// <summary>
        /// 关机按钮点击事件处理
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private async void Button_Click_3(object sender, RoutedEventArgs e)
        {
            await ExecuteDeviceActionAsync(sender as Button, "关机", "正在关机...",
                async () => await _backgroundThread._adbClient.RebootAsync(BootState.Poweroff));
        }

        /// <summary>
        /// 执行设备操作的通用方法，处理按钮状态和错误
        /// </summary>
        /// <param name="button">触发操作的按钮</param>
        /// <param name="originalContent">按钮原始文本</param>
        /// <param name="processingContent">操作执行中的按钮文本</param>
        /// <param name="action">要执行的异步操作</param>
        /// <returns>表示异步操作的任务</returns>
        private async Task ExecuteDeviceActionAsync(Button button, string originalContent, string processingContent, Func<Task> action)
        {
            if (button == null) return;

            // 保存原始按钮文本并禁用按钮，防止重复点击
            button.IsEnabled = false;
            string buttonText = button.Content as string ?? originalContent;
            button.Content = processingContent;

            try
            {

                // 检查ADB,fastboot客户端是否初始化
                if (_backgroundThread == null || _backgroundThread._adbClient == null || _backgroundThread._fastbootClient == null)
                {
                    MessageBox.Show("ADB|fastboot未初始化，请检查应用配置。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 执行操作并设置30秒超时，防止界面卡死
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                var actionTask = action();

                // 等待任意一个任务完成
                var completedTask = await Task.WhenAny(actionTask, timeoutTask);

                // 如果是超时任务先完成，则抛出超时异常
                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException("操作超时，请检查设备连接状态。");
                }

                // 等待操作任务完成，以便传播任何可能的异常
                await actionTask;
            }
            catch (Exception ex)
            {
                // 显示操作过程中的错误信息
                MessageBox.Show($"执行操作时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 恢复按钮状态
                button.Content = buttonText;
                button.IsEnabled = true;
            }
        }

        #region IDisposable实现

        /// <summary>
        /// 释放资源的受保护方法
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源（定时器）
                    _deviceCheckTimer?.Dispose();
                    _deviceCheckTimer = null;
                }

                // 标记为已释放
                _disposed = true;
            }
        }

        /// <summary>
        /// 实现IDisposable接口的Dispose方法，用于释放资源
        /// </summary>
        public void Dispose()
        {
            // 释放资源
            Dispose(true);
            // 通知GC不再需要调用终结器
            GC.SuppressFinalize(this);
        }

        #endregion
        //目标模式: 0=系统, 1=引导模式, 2=恢复模式, 3=9008模式
        private async void Button_Click_4(object sender, RoutedEventArgs e)
        {
            await ExecuteDeviceActionAsync(sender as Button, "重启设备", "正在重启...",
                async () => await _backgroundThread._fastbootClient.RebootAsync(0));
        }

        private async void Button_Click_5(object sender, RoutedEventArgs e)
        {
            await ExecuteDeviceActionAsync(sender as Button, "Recovery 模式", "正在重启到 Recovery...",
              async () => await _backgroundThread._fastbootClient.RebootAsync(1));
        }

        private async void Button_Click_6(object sender, RoutedEventArgs e)
        {
            await ExecuteDeviceActionAsync(sender as Button, "Bootloader 模式", "正在重启到 Bootloader...",
              async () => await _backgroundThread._fastbootClient.RebootAsync(2));
        }

        private async void Button_Click_7(object sender, RoutedEventArgs e)
        {
            await ExecuteDeviceActionAsync(sender as Button, "9008", "正在重启到 9008",
              async () => await _backgroundThread._fastbootClient.RebootAsync(3));
        }
    }
}