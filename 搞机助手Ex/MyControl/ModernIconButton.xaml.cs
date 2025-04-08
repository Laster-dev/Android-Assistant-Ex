using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace 搞机助手Ex.MyControl
{
    public partial class ModernIconButton : UserControl
    {
        // 图标依赖属性
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register("Icon", typeof(object), typeof(ModernIconButton), new PropertyMetadata(null));

        // 文本依赖属性
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(ModernIconButton), new PropertyMetadata(string.Empty));

        // 悬停背景依赖属性
        public static readonly DependencyProperty HoverBackgroundProperty =
            DependencyProperty.Register("HoverBackground", typeof(Brush), typeof(ModernIconButton),
                new PropertyMetadata(new SolidColorBrush(Color.FromArgb(20, 0, 0, 0))));

        // 按下背景依赖属性
        public static readonly DependencyProperty PressedBackgroundProperty =
            DependencyProperty.Register("PressedBackground", typeof(Brush), typeof(ModernIconButton),
                new PropertyMetadata(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0))));

        // 点击命令依赖属性
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register("Command", typeof(ICommand), typeof(ModernIconButton), new PropertyMetadata(null));

        // 命令参数依赖属性
        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register("CommandParameter", typeof(object), typeof(ModernIconButton), new PropertyMetadata(null));

        // 字体大小依赖属性
        public static new readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register("FontSize", typeof(double), typeof(ModernIconButton), new PropertyMetadata(12.0));

        // 前景色依赖属性
        public static new readonly DependencyProperty ForegroundProperty =
            DependencyProperty.Register("Foreground", typeof(Brush), typeof(ModernIconButton),
                new PropertyMetadata(new SolidColorBrush(Colors.Black)));

        // 字体粗细依赖属性
        public static new readonly DependencyProperty FontWeightProperty =
            DependencyProperty.Register("FontWeight", typeof(FontWeight), typeof(ModernIconButton),
                new PropertyMetadata(FontWeights.Normal));

        // 图标
        public object Icon
        {
            get { return GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        // 文本
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        // 鼠标悬停时的背景色
        public Brush HoverBackground
        {
            get { return (Brush)GetValue(HoverBackgroundProperty); }
            set { SetValue(HoverBackgroundProperty, value); }
        }

        // 鼠标按下时的背景色
        public Brush PressedBackground
        {
            get { return (Brush)GetValue(PressedBackgroundProperty); }
            set { SetValue(PressedBackgroundProperty, value); }
        }

        // 命令
        public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        // 命令参数
        public object CommandParameter
        {
            get { return GetValue(CommandParameterProperty); }
            set { SetValue(CommandParameterProperty, value); }
        }

        // 字体大小
        public new double FontSize
        {
            get { return (double)GetValue(FontSizeProperty); }
            set { SetValue(FontSizeProperty, value); }
        }

        // 前景色
        public new Brush Foreground
        {
            get { return (Brush)GetValue(ForegroundProperty); }
            set { SetValue(ForegroundProperty, value); }
        }

        // 字体粗细
        public new FontWeight FontWeight
        {
            get { return (FontWeight)GetValue(FontWeightProperty); }
            set { SetValue(FontWeightProperty, value); }
        }

        // 点击事件
        public event RoutedEventHandler Click;

        public ModernIconButton()
        {
            InitializeComponent();

        }

        private void MainButton_Click(object sender, RoutedEventArgs e)
        {
            // 执行命令
            if (Command != null && Command.CanExecute(CommandParameter))
            {
                Command.Execute(CommandParameter);
            }

            // 触发点击事件
            Click?.Invoke(this, e);

        }
    }
}