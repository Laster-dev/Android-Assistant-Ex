using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace 搞机助手Ex.MyControl
{
    public class HorizontalBatteryControl : UserControl
    {
        private Canvas _canvas;
        private Rectangle _batteryBody;
        private Rectangle _batteryTip;
        private Rectangle _batteryLevel;

        #region Dependency Properties  

        public static readonly DependencyProperty CapacityProperty =
            DependencyProperty.Register("Capacity", typeof(double), typeof(HorizontalBatteryControl),
                new PropertyMetadata(100.0, OnCapacityChanged));

        public static readonly DependencyProperty AnimateChangesProperty =
            DependencyProperty.Register("AnimateChanges", typeof(bool), typeof(HorizontalBatteryControl),
                new PropertyMetadata(true));

        public double Capacity
        {
            get { return (double)GetValue(CapacityProperty); }
            set { SetValue(CapacityProperty, Clamp(value, 0, 100)); }
        }

        public bool AnimateChanges
        {
            get { return (bool)GetValue(AnimateChangesProperty); }
            set { SetValue(AnimateChangesProperty, value); }
        }

        #endregion

        public HorizontalBatteryControl()
        {
            _canvas = new Canvas();
            this.Content = _canvas;

            // 初始化电池组件  
            InitializeBatteryComponents();

            // 注册尺寸变化事件，以便在大小改变时重绘电池  
            this.SizeChanged += HorizontalBatteryControl_SizeChanged;
        }

        private void InitializeBatteryComponents()
        {
            // 电池主体  
            _batteryBody = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                RadiusX = 3,
                RadiusY = 3
            };
            _canvas.Children.Add(_batteryBody);

            // 电池正极突出部分  
            _batteryTip = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromRgb(80, 80, 80))
            };
            _canvas.Children.Add(_batteryTip);

            // 电池电量显示  
            _batteryLevel = new Rectangle
            {
                Fill = new SolidColorBrush(Colors.Green),
                RadiusX = 2,
                RadiusY = 2
            };
            _canvas.Children.Add(_batteryLevel);
        }

        private void HorizontalBatteryControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateBatteryLayout();
            UpdateCapacityDisplay(Capacity, true);
        }

        private void UpdateBatteryLayout()
        {
            double padding = 2;
            double innerPadding = 3;
            double tipWidth = ActualWidth * 0.05;
            double tipHeight = ActualHeight * 0.4;

            // 设置电池主体位置和大小  
            double bodyWidth = ActualWidth - tipWidth - padding * 2;
            Canvas.SetLeft(_batteryBody, padding);
            Canvas.SetTop(_batteryBody, padding);
            _batteryBody.Width = bodyWidth;
            _batteryBody.Height = ActualHeight - padding * 2;

            // 设置正极突出部分位置和大小  
            Canvas.SetLeft(_batteryTip, bodyWidth + padding);
            Canvas.SetTop(_batteryTip, (ActualHeight - tipHeight) / 2);
            _batteryTip.Width = tipWidth;
            _batteryTip.Height = tipHeight;

            // 设置电量显示的基本位置  
            Canvas.SetLeft(_batteryLevel, padding + innerPadding);
            Canvas.SetTop(_batteryLevel, padding + innerPadding);
            _batteryLevel.Height = ActualHeight - (padding + innerPadding) * 2;
        }

        private static void OnCapacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            HorizontalBatteryControl control = (HorizontalBatteryControl)d;
            double newValue = Clamp((double)e.NewValue, 0, 100);
            control.UpdateCapacityDisplay(newValue);
        }

        private void UpdateCapacityDisplay(double capacity, bool forceNoAnimation = false)
        {
            if (_batteryBody.ActualWidth <= 0)
                return;

            // 计算电池填充宽度  
            double innerPadding = 3;
            double maxWidth = _batteryBody.Width - innerPadding * 2;
            double levelWidth = (maxWidth * capacity / 100.0);

            // 应用动画或直接设置宽度  
            if (AnimateChanges && !forceNoAnimation)
            {
                DoubleAnimation animation = new DoubleAnimation(
                    _batteryLevel.Width,
                    levelWidth,
                    TimeSpan.FromMilliseconds(300));
                _batteryLevel.BeginAnimation(WidthProperty, animation);
            }
            else
            {
                _batteryLevel.Width = levelWidth;
            }

            // 根据容量更新颜色 - 从红色(低)到绿色(高)
            Color levelColor = GetColorForCapacity(capacity);
            _batteryLevel.Fill = new SolidColorBrush(levelColor);
        }
        private Color GetColorForCapacity(double capacity)
        {
            if (capacity <= 50)
            {
                // 在红色 (249,67,70) 和黄色 (249,197,78) 之间插值
                double factor = capacity / 50.0;
                return Color.FromRgb(
                    249,
                    (byte)(67 + (197 - 67) * factor),
                    (byte)(70 + (78 - 70) * factor)
                );
            }
            else
            {
                // 在黄色 (249,197,78) 和蓝色 (73,143,218) 之间插值
                double factor = (capacity - 50) / 50.0;
                return Color.FromRgb(
                    (byte)(249 + (73 - 249) * factor),
                    (byte)(197 + (143 - 197) * factor),
                    (byte)(78 + (218 - 78) * factor)
                );
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}