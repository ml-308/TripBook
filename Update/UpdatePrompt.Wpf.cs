// ============================================================================
//  UpdatePrompt.Wpf.cs — 「发现新版本」提示窗（WPF）
//  与 UpdateCheck.cs 配套；启动后延迟检查，发现新版本时弹窗提示。
// ============================================================================
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace UpdateKit
{
    internal static class UpdatePrompt
    {
        /// <summary>启动后自动检查（最多每 24 小时一次；--check-update 时立即检查）。</summary>
        public static void ScheduleAutoCheck(Window owner)
        {
            if (owner == null || !UpdateCheck.AutoEnabled) return;
            if (!UpdateCheck.ForceNow && !UpdateCheck.ShouldAutoCheck()) return;
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(2200);
            timer.Tick += delegate
            {
                timer.Stop();
                Run(owner, !UpdateCheck.ForceNow);
            };
            timer.Start();
        }

        /// <summary>菜单「检查更新」用：立刻检查，已是最新也给反馈。</summary>
        public static void CheckNow(Window owner)
        {
            if (owner == null) return;
            Run(owner, false);
        }

        static void Run(Window owner, bool quietWhenUpToDate)
        {
            UpdateCheck.CheckAsync(delegate(UpdateInfo info)
            {
                if (owner == null) return;
                try
                {
                    owner.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                    {
                        Handle(owner, info, quietWhenUpToDate);
                    }));
                }
                catch (Exception) { }
            });
        }

        static void Handle(Window owner, UpdateInfo info, bool quietWhenUpToDate)
        {
            try
            {
                if (!info.Ok)
                {
                    if (!quietWhenUpToDate)
                        MessageBox.Show(owner, "暂时无法检查更新。\n\n" + (info.Error ?? "未知错误") +
                            "\n\n可以稍后在菜单里重试，或直接访问：\n" + UpdateCheck.ReleasesUrl,
                            UpdateCheck.AppName + " - 检查更新", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (!info.HasUpdate)
                {
                    if (!quietWhenUpToDate)
                        MessageBox.Show(owner, "当前已是最新版本 v" + info.Current + "。",
                            UpdateCheck.AppName + " - 检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (quietWhenUpToDate && string.Equals(info.Latest, UpdateCheck.SkippedVersion(), StringComparison.Ordinal))
                    return;

                UpdateDialog dlg = new UpdateDialog(info);
                if (owner != null && owner.IsVisible) dlg.Owner = owner;
                dlg.ShowDialog();
            }
            catch (Exception) { }
        }
    }

    /// <summary>发现新版本时的提示窗：立即下载 / 稍后提醒 / 跳过此版本。</summary>
    internal sealed class UpdateDialog : Window
    {
        readonly UpdateInfo _info;

        static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(26, 26, 30));
        static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(120, 122, 130));
        static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(10, 132, 255));

        public UpdateDialog(UpdateInfo info)
        {
            _info = info;

            Title = UpdateCheck.AppName + " - 发现新版本";
            Width = 580;
            Height = 520;
            MinWidth = 580;
            MinHeight = 440;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = true;
            Background = Brushes.White;
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI, Arial");
            FontSize = 12.5;

            // 自由高度：说明用 Grid 的星号行吃掉剩余空间，按钮永远固定在底部。
            Grid root = new Grid();
            root.Margin = new Thickness(26, 22, 26, 18);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock title = new TextBlock();
            title.Text = "发现新版本 v" + info.Latest;
            title.FontSize = 20;
            title.FontWeight = FontWeights.SemiBold;
            title.Foreground = Ink;
            Grid.SetRow(title, 0);
            root.Children.Add(title);

            TextBlock sub = new TextBlock();
            string name = string.IsNullOrEmpty(info.ReleaseName) ? "" : Trim(info.ReleaseName, 60);
            sub.Text = "当前版本 v" + info.Current + "  →  最新版本 v" + info.Latest +
                       (name.Length > 0 ? "\n" + name : "");
            sub.Foreground = Muted;
            sub.Margin = new Thickness(0, 8, 0, 0);
            sub.TextWrapping = TextWrapping.Wrap;
            Grid.SetRow(sub, 1);
            root.Children.Add(sub);

            string published = FormatDate(info.PublishedAt);
            if (published.Length > 0)
            {
                TextBlock date = new TextBlock();
                date.Text = "发布于 " + published + "  ·  " + UpdateCheck.Owner + "/" + UpdateCheck.RepoSlug;
                date.Foreground = Muted;
                date.FontSize = 11.5;
                date.Margin = new Thickness(0, 6, 0, 0);
                Grid.SetRow(date, 2);
                root.Children.Add(date);
            }

            TextBlock notesLabel = new TextBlock();
            notesLabel.Text = "更新内容";
            notesLabel.Foreground = Ink;
            notesLabel.FontWeight = FontWeights.SemiBold;
            notesLabel.Margin = new Thickness(0, 16, 0, 6);
            Grid.SetRow(notesLabel, 3);
            root.Children.Add(notesLabel);

            TextBox notes = new TextBox();
            notes.Text = NotesText(info.Notes);
            notes.IsReadOnly = true;
            notes.AcceptsReturn = true;
            notes.TextWrapping = TextWrapping.Wrap;
            notes.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            notes.MinHeight = 140;
            notes.VerticalAlignment = VerticalAlignment.Stretch;
            notes.Padding = new Thickness(8);
            notes.FontSize = 12.5;
            notes.Background = new SolidColorBrush(Color.FromRgb(247, 248, 250));
            notes.BorderBrush = new SolidColorBrush(Color.FromRgb(222, 224, 230));
            notes.Foreground = Ink;
            Grid.SetRow(notes, 4);
            root.Children.Add(notes);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 16, 0, 0);

            Button skip = MakeButton("跳过此版本", 120, false);
            skip.Click += delegate { UpdateCheck.SkipVersion(_info.Latest); Close(); };
            skip.Margin = new Thickness(0, 0, 8, 0);
            buttons.Children.Add(skip);

            Button later = MakeButton("稍后提醒", 96, false);
            later.Click += delegate { Close(); };
            later.Margin = new Thickness(0, 0, 8, 0);
            buttons.Children.Add(later);

            Button download = MakeButton("立即下载", 96, true);
            download.Click += delegate { UpdateCheck.OpenReleases(); Close(); };
            buttons.Children.Add(download);

            Grid.SetRow(buttons, 5);
            root.Children.Add(buttons);

            Content = root;

            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) Close();
            };
            Loaded += delegate { download.Focus(); };
        }

        static Button MakeButton(string text, double width, bool accent)
        {
            Button b = new Button();
            b.Content = text;
            b.Width = width;
            b.Height = 34;
            b.FontWeight = FontWeights.SemiBold;
            b.Cursor = Cursors.Hand;
            if (accent)
            {
                b.Background = Accent;
                b.Foreground = Brushes.White;
                b.BorderThickness = new Thickness(0);
            }
            return b;
        }

        static string NotesText(string notes)
        {
            if (string.IsNullOrEmpty(notes)) return "（本版本未提供更新说明，详见 Release 页面。）";
            string text = notes.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (text.Length > 2400) text = text.Substring(0, 2400) + "\n…";
            return text;
        }

        static string FormatDate(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "";
            DateTime parsed;
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed))
                return parsed.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return "";
        }

        static string Trim(string text, int max)
        {
            if (text == null) return "";
            text = text.Trim();
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }
    }
}
