using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace TripBook
{
    /// <summary>深色风格的提示 / 确认对话框</summary>
    public static class Dialogs
    {
        public static void Info(Window owner, string title, string message)
        {
            Build(owner, title, message, "知道了", false, false);
        }

        public static bool Confirm(Window owner, string title, string message,
                                   string okText = "确定", bool danger = false)
        {
            return Build(owner, title, message, okText, true, danger);
        }

        static bool Build(Window owner, string title, string message, string okText,
                          bool withCancel, bool danger)
        {
            var win = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                SizeToContent = SizeToContent.Height,
                Width = 440,
                WindowStartupLocation = owner != null
                    ? WindowStartupLocation.CenterOwner
                    : WindowStartupLocation.CenterScreen,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize,
                Owner = owner
            };

            var root = new Border
            {
                Background = Palette.Brush("#151A22"),
                BorderBrush = Palette.Brush(Palette.BorderHi),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(22, 20, 22, 18),
                Margin = new Thickness(14),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black, BlurRadius = 28, ShadowDepth = 6, Opacity = 0.55
                }
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Palette.Brush(Palette.Text)
            });
            stack.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 13,
                Foreground = Palette.Brush(Palette.TextSub),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 0),
                LineHeight = 22
            });

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            if (withCancel)
            {
                var cancel = MakeButton("取消", false, false);
                cancel.Margin = new Thickness(0, 0, 8, 0);
                cancel.Click += (s, e) => win.DialogResult = false;
                buttons.Children.Add(cancel);
            }

            var ok = MakeButton(okText, true, danger);
            ok.Click += (s, e) => win.DialogResult = true;
            buttons.Children.Add(ok);

            stack.Children.Add(buttons);
            root.Child = stack;
            win.Content = root;

            root.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed) win.DragMove();
            };

            win.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) win.DialogResult = false;
                else if (e.Key == Key.Enter) win.DialogResult = true;
            };

            return win.ShowDialog() == true;
        }

        static Button MakeButton(string text, bool primary, bool danger)
        {
            var b = new Button
            {
                Content = text,
                Height = 34,
                MinWidth = 84,
                Cursor = Cursors.Hand,
                FontSize = 13,
                Padding = new Thickness(16, 0, 16, 0)
            };
            if (primary)
            {
                var color = danger ? "#E4576D" : Palette.Accent;
                b.Background = Palette.Brush(color);
                b.Foreground = Brushes.White;
                b.BorderBrush = Palette.Brush(color);
            }
            else
            {
                b.Background = Palette.Brush(Palette.Card);
                b.Foreground = Palette.Brush(Palette.Text);
                b.BorderBrush = Palette.Brush(Palette.Border);
            }
            b.Template = (ControlTemplate)BuildTemplate(primary, danger);
            b.FocusVisualStyle = null;
            return b;
        }

        static object BuildTemplate(bool primary, bool danger)
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetBinding(Border.BackgroundProperty,
                new System.Windows.Data.Binding("Background")
                { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty,
                new System.Windows.Data.Binding("BorderBrush")
                { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.PaddingProperty, new Thickness(16, 0, 16, 0));

            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);

            var t = new ControlTemplate(typeof(Button)) { VisualTree = border };
            return t;
        }
    }
}
