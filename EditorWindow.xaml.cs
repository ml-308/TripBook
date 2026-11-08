using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TripBook
{
    public partial class EditorWindow : Window
    {
        public Journey Result { get; private set; }

        readonly Journey _editing;
        readonly DispatcherTimer _timer;
        bool _titleTouched;
        bool _dateTouched;
        bool _syncing;

        public EditorWindow(Journey existing)
        {
            InitializeComponent();
            _editing = existing;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(230) };
            _timer.Tick += (s, e) => { _timer.Stop(); ParseNow(); };

            if (existing != null)
            {
                HeaderText.Text = "编辑行程";
                Title = "编辑行程";
                _titleTouched = true;
                _dateTouched = true;
                DateBox.Text = existing.date;
                TitleBox.Text = existing.title;
                Editor.Text = JourneyText.ToText(existing);
            }
            else
            {
                var t = DateTime.Today;
                DateBox.Text = t.ToString("yyyy-MM-dd");
                TitleBox.Text = t.Month + "." + t.Day + " 行程";
            }

            Loaded += (s, e) =>
            {
                Editor.Focus();
                Editor.CaretIndex = Editor.Text.Length;
                ParseNow();
            };
        }

        void Window_SourceInitialized(object sender, EventArgs e)
        {
            Native.DarkTitleBar(this);
        }

        // -------------------------------------------------- 交互

        static DateTime? ParseDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim().Replace('/', '-').Replace('.', '-').Replace('年', '-').Replace('月', '-').Replace("日", "");
            DateTime d;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return d.Date;
            var parts = s.Split('-');
            if (parts.Length == 2)
            {
                int mo, dy;
                if (int.TryParse(parts[0], out mo) && int.TryParse(parts[1], out dy))
                {
                    try { return new DateTime(DateTime.Today.Year, mo, dy); } catch { }
                }
            }
            return null;
        }

        void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            _timer.Stop();
            _timer.Start();
        }

        void TitleBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing) return;
            _titleTouched = true;
            if (_dateTouched) return;
            var d = ItineraryParser.TitleToDate(TitleBox.Text, DateTime.Today);
            if (d != null)
            {
                _syncing = true;
                DateBox.Text = d.Value.ToString("yyyy-MM-dd");
                _syncing = false;
            }
        }

        void DateBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing) return;
            _dateTouched = true;
        }

        void Paste_Click(object sender, RoutedEventArgs e)
        {
            string txt = "";
            try { txt = Clipboard.GetText(); } catch { }
            if (string.IsNullOrWhiteSpace(txt)) { StatusText.Text = "剪贴板里没有文本"; return; }
            Editor.Text = txt;
            Editor.Focus();
        }

        void Sample_Click(object sender, RoutedEventArgs e)
        {
            Editor.Text = ItineraryParser.SampleText;
            _titleTouched = false;
            _dateTouched = false;
            ParseNow();
        }

        // -------------------------------------------------- 解析预览

        void ParseNow()
        {
            var text = Editor.Text ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                StatusText.Text = "等待输入…";
                StatusText.Foreground = Palette.Brush(Palette.TextDim);
                PreviewPanel.Visibility = Visibility.Collapsed;
                return;
            }

            var d = ParseDate(DateBox.Text) ?? DateTime.Today;
            var j = ItineraryParser.Parse(text, d);

            if (!_titleTouched && !string.IsNullOrEmpty(j.title))
            {
                _syncing = true;
                if (TitleBox.Text != j.title) TitleBox.Text = j.title;
                if (!_dateTouched)
                {
                    var dd = ParseDate(j.date);
                    if (dd != null && DateBox.Text != dd.Value.ToString("yyyy-MM-dd"))
                        DateBox.Text = dd.Value.ToString("yyyy-MM-dd");
                }
                _syncing = false;
            }

            int n = j.entries.Count, np = j.pov.Count;
            if (n == 0)
            {
                StatusText.Text = "⚠ 还没识别到行程记录 —— 每行要以 HH:MM 时间开头，例如 12:00 人民中路上车 611_65028 2.00¥";
                StatusText.Foreground = Palette.Brush(Palette.Amber);
                PreviewPanel.Visibility = Visibility.Collapsed;
                return;
            }

            var st = new JourneyStats(j.entries);
            var markerTxt = st.MarkerCount > 0 ? " · " + st.MarkerCount + " 个标记" : "";
            StatusText.Text = "✓ 识别到 " + n + " 条记录 · " + np + " 条 POV · 时长 " +
                              Fmt.Duration(st.DurationMin) + " · 花费 ¥" + Fmt.Money(st.Fare) + markerTxt;
            StatusText.Foreground = Palette.Brush(Palette.Teal);
            BuildPreview(j);
        }

        void BuildPreview(Journey j)
        {
            PreviewStack.Children.Clear();
            int show = Math.Min(3, j.entries.Count);
            for (int i = 0; i < show; i++)
            {
                var e = j.entries[i];
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var t = new TextBlock
                {
                    Text = e.time,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = Palette.Brush(Palette.Accent),
                    FontSize = 12
                };
                Grid.SetColumn(t, 0);
                row.Children.Add(t);

                var s = new TextBlock
                {
                    Text = (string.IsNullOrEmpty(e.station) ? "—" : e.station) +
                           (string.IsNullOrEmpty(e.line) ? "" : "  " + e.line),
                    Foreground = Palette.Brush(Palette.TextSub),
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(s, 1);
                row.Children.Add(s);

                if (!string.IsNullOrEmpty(e.vehicle))
                {
                    var v = new TextBlock
                    {
                        Text = e.vehicle,
                        FontFamily = new FontFamily("Consolas"),
                        Foreground = Palette.Brush(Palette.TextDim),
                        FontSize = 12
                    };
                    Grid.SetColumn(v, 2);
                    row.Children.Add(v);
                }

                PreviewStack.Children.Add(row);
            }

            if (j.entries.Count > show)
            {
                PreviewStack.Children.Add(new TextBlock
                {
                    Text = "… 还有 " + (j.entries.Count - show) + " 条",
                    FontSize = 11,
                    Foreground = Palette.Brush(Palette.TextDim),
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            PreviewPanel.Visibility = Visibility.Visible;
        }

        // -------------------------------------------------- 保存

        void Save_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }

        void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        void Save()
        {
            var text = (Editor.Text ?? "").Trim();
            if (text == "")
            {
                Dialogs.Info(this, "还没内容", "请先粘贴或输入行程文本。");
                return;
            }

            var date = ParseDate(DateBox.Text) ?? DateTime.Today;
            var j = ItineraryParser.Parse(text, date);
            if (j.entries.Count == 0)
            {
                Dialogs.Info(this, "解析失败",
                    "没有识别到任何行程记录。\n每行需要以 HH:MM 时间开头，例如：\n12:00 人民中路上车 611_65028 2.00¥");
                return;
            }

            var title = (TitleBox.Text ?? "").Trim();
            j.title = title != "" ? title : j.title;
            j.date = date.ToString("yyyy-MM-dd");
            j.source_text = text;

            if (_editing != null)
            {
                j.id = _editing.id;
                j.created_at = _editing.created_at;
            }

            Result = j;
            DialogResult = true;
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Key == System.Windows.Input.Key.S &&
                System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                Save();
                e.Handled = true;
            }
        }
    }
}
