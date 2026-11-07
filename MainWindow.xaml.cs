using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace TripBook
{
    public partial class MainWindow : Window
    {
        readonly JourneyStore _store;
        readonly ObservableCollection<ListItemVm> _items = new ObservableCollection<ListItemVm>();
        readonly DispatcherTimer _toastTimer;
        string _currentId = "";
        string _filter = "";

        public MainWindow()
        {
            InitializeComponent();
            _store = new JourneyStore();
            JourneyList.ItemsSource = _items;

            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2600) };
            _toastTimer.Tick += (s, e) => { _toastTimer.Stop(); Toast.Visibility = Visibility.Collapsed; };

            RestoreWindow();
            RefreshList(null);
            if (!string.IsNullOrEmpty(_store.LastError))
                Dispatcher.BeginInvoke(new Action(() => ShowToast(_store.LastError)), DispatcherPriority.Background);
        }

        // -------------------------------------------------- 初始化

        void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyDarkTitleBar();
            UpdateDirLabel();
        }

        void ApplyDarkTitleBar()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                int on = 1;
                if (DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, 19, ref on, sizeof(int));
            }
            catch { }
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        // -------------------------------------------------- 列表

        void RefreshList(string selectId, string restoreFilter = null)
        {
            if (restoreFilter != null) _filter = restoreFilter;
            var keep = selectId ?? _currentId;

            _items.Clear();
            foreach (var j in _store.Journeys)
                if (Matches(j)) _items.Add(ListItemVm.Build(j));

            CountLabel.Text = _store.Journeys.Count == 0
                ? "暂无数据"
                : "共 " + _store.Journeys.Count + " 条行程" + (_filter != "" ? " · 匹配 " + _items.Count : "");

            if (_items.Count == 0)
            {
                _currentId = "";
                ShowEmpty(_filter != "");
                return;
            }

            var target = _items.FirstOrDefault(x => x.J.id == keep) ?? _items[0];
            JourneyList.SelectedItem = target;
            _currentId = target.J.id;
            ShowJourney(target.J);
        }

        bool Matches(Journey j)
        {
            if (string.IsNullOrEmpty(_filter)) return true;
            var q = _filter.ToLowerInvariant();
            if ((j.title ?? "").ToLowerInvariant().Contains(q)) return true;
            if ((j.date ?? "").Contains(q)) return true;
            foreach (var e in j.entries)
            {
                if ((e.station ?? "").ToLowerInvariant().Contains(q)) return true;
                if ((e.line ?? "").ToLowerInvariant().Contains(q)) return true;
                if ((e.vehicle ?? "").ToLowerInvariant().Contains(q)) return true;
            }
            return false;
        }

        void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            _filter = SearchBox.Text.Trim();
            RefreshList(null);
        }

        void JourneyList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = JourneyList.SelectedItem as ListItemVm;
            if (vm == null) { ShowEmpty(false); return; }
            _currentId = vm.J.id;
            _store.Config.last_id = _currentId;
            _store.SaveConfig();
            ShowJourney(vm.J);
        }

        void ShowJourney(Journey j)
        {
            DetailScroll.DataContext = JourneyVm.Build(j);
            DetailScroll.Visibility = Visibility.Visible;
            EmptyPanel.Visibility = Visibility.Collapsed;
            DetailScroll.ScrollToTop();
        }

        void ShowEmpty(bool searching)
        {
            DetailScroll.Visibility = Visibility.Collapsed;
            EmptyPanel.Visibility = Visibility.Visible;
            EmptyTitle.Text = searching ? "没有匹配的行程" : "还没有行程记录";
            EmptySub.Text = searching
                ? "换个关键词，或者清空搜索框看全部"
                : "粘贴随手记的行程文本，自动整理成时间轴、线路链与统计";
            SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        void UpdateDirLabel()
        {
            DirLabel.Text = _store.DataDir;
            DirLabel.ToolTip = _store.DataDir;
        }

        // -------------------------------------------------- 动作

        void New_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new EditorWindow(null) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                var j = _store.Add(dlg.Result);
                RefreshList(j.id);
                ShowToast("已保存「" + j.title + "」");
            }
        }

        void Edit_Click(object sender, RoutedEventArgs e)
        {
            var j = _store.Get(_currentId);
            if (j == null) return;
            var dlg = new EditorWindow(j) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                var updated = _store.Update(dlg.Result);
                RefreshList(updated.id);
                ShowToast("已更新");
            }
        }

        void Sample_Click(object sender, RoutedEventArgs e)
        {
            var j = _store.Add(ItineraryParser.Parse(ItineraryParser.SampleText));
            RefreshList(j.id);
            ShowToast("已载入示例行程");
        }

        void Export_Click(object sender, RoutedEventArgs e)
        {
            var j = _store.Get(_currentId);
            if (j == null) return;
            ExportJourney(j);
        }

        void ExportJourney(Journey j)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出网页",
                Filter = "HTML 文件 (*.html)|*.html",
                FileName = j.date.Replace("-", "") + "_" + j.title.Replace(" ", "") + ".html"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                File.WriteAllText(dlg.FileName, HtmlExporter.Build(j), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Dialogs.Info(this, "导出失败", ex.Message);
                return;
            }
            ShowToast("已导出：" + dlg.FileName);
            try { Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true }); } catch { }
        }

        void Copy_Click(object sender, RoutedEventArgs e)
        {
            var j = _store.Get(_currentId);
            if (j == null) return;
            try { Clipboard.SetText(JourneyText.ToText(j)); ShowToast("行程文本已复制"); }
            catch { ShowToast("复制失败"); }
        }

        void Delete_Click(object sender, RoutedEventArgs e)
        {
            var j = _store.Get(_currentId);
            if (j == null) return;
            if (!Dialogs.Confirm(this, "删除行程",
                    "确定删除「" + j.title + "」？\n此操作不可撤销。", "删除", danger: true))
                return;
            _store.Delete(_currentId);
            _currentId = "";
            RefreshList(null);
            ShowToast("已删除");
        }

        void More_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            menu.Items.Add(MenuItemOf("复制行程文本", Copy_Click));
            menu.Items.Add(MenuItemOf("导出网页 (HTML)", (s, a) => Export_Click(s, a)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItemOf("删除这条行程", Delete_Click));
            menu.PlacementTarget = (UIElement)sender;
            menu.IsOpen = true;
        }

        static MenuItem MenuItemOf(string text, RoutedEventHandler handler)
        {
            var mi = new MenuItem { Header = text };
            mi.Click += handler;
            return mi;
        }

        // -------------------------------------------------- 数据目录

        void DataDir_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            menu.Items.Add(MenuItemOf("打开数据目录", (s, a) => OpenDataDir()));
            menu.Items.Add(MenuItemOf("更改数据目录…", (s, a) => ChangeDataDir()));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItemOf("导入其他数据文件…", (s, a) => ImportData()));
            menu.Items.Add(MenuItemOf("导出全部备份…", (s, a) => ExportAll()));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItemOf("刷新 (F5)", (s, a) => Reload(false)));
            menu.Items.Add(MenuItemOf("关于", (s, a) => About()));
            menu.PlacementTarget = (UIElement)sender;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            menu.IsOpen = true;
        }

        void OpenDataDir()
        {
            try { Process.Start("explorer.exe", "\"" + _store.DataDir + "\""); }
            catch { Dialogs.Info(this, "数据目录", _store.DataDir); }
        }

        void ChangeDataDir()
        {
            using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
            {
                fbd.Description = "选择行程数据存放目录";
                fbd.SelectedPath = _store.DataDir;
                if (fbd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                var newDir = fbd.SelectedPath;
                if (string.Equals(Path.GetFullPath(newDir), Path.GetFullPath(_store.DataDir),
                        StringComparison.OrdinalIgnoreCase)) return;

                var choice = Dialogs.Confirm(this, "切换数据目录",
                    "把当前 " + _store.Journeys.Count + " 条行程一起复制到新目录吗？\n\n" +
                    "「复制过去」：保留现有数据并合并新目录里的数据\n" +
                    "「取消」：不做任何更改",
                    "复制过去");
                if (!choice) return;

                string err;
                if (!_store.ChangeDir(newDir, true, out err))
                {
                    Dialogs.Info(this, "切换失败", err);
                    return;
                }
                UpdateDirLabel();
                _currentId = "";
                RefreshList(null);
                ShowToast("数据目录已切换");
            }
        }

        void ImportData()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择数据文件",
                Filter = "JSON 文件 (*.json)|*.json",
                InitialDirectory = _store.DataDir
            };
            if (dlg.ShowDialog(this) != true) return;
            string err;
            int n = _store.ImportFile(dlg.FileName, out err);
            if (!string.IsNullOrEmpty(err)) { Dialogs.Info(this, "导入失败", err); return; }
            RefreshList(null);
            ShowToast("已导入 " + n + " 条行程");
        }

        void ExportAll()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出全部备份",
                Filter = "JSON 文件 (*.json)|*.json",
                FileName = "TripBook备份_" + DateTime.Today.ToString("yyyy-MM-dd") + ".json"
            };
            if (dlg.ShowDialog(this) != true) return;
            string err;
            if (!_store.ExportAll(dlg.FileName, out err)) { Dialogs.Info(this, "导出失败", err); return; }
            ShowToast("备份已导出");
        }

        void Reload(bool quiet)
        {
            _store.Reload();
            RefreshList(null);
            if (!quiet) ShowToast("已刷新");
        }

        void About()
        {
            Dialogs.Info(this, "关于",
                "行程簿 TripBook 1.0\n\n本地行程记录工具\n" +
                "· 数据保存在：" + _store.DataDir + "\n" +
                "· 粘贴文本自动解析、时间轴展示、网页导出\n\n" +
                "快捷键：Ctrl+N 新建 · Ctrl+F 搜索 · Ctrl+E 导出 · F5 刷新");
        }

        // -------------------------------------------------- 提示条

        public void ShowToast(string text)
        {
            ToastText.Text = text;
            Toast.Visibility = Visibility.Visible;
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        // -------------------------------------------------- 窗口状态

        void RestoreWindow()
        {
            var w = _store.Config.window;
            if (w == null) return;
            if (w.w > 400 && w.h > 300)
            {
                Width = w.w;
                Height = w.h;
            }
            if (w.x.HasValue && w.y.HasValue &&
                w.x.Value > SystemParameters.VirtualScreenLeft - 50 &&
                w.y.Value > SystemParameters.VirtualScreenTop - 50)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = w.x.Value;
                Top = w.y.Value;
            }
            if (w.max) WindowState = System.Windows.WindowState.Maximized;
        }

        void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _store.Config.last_id = _currentId;
            var w = new WindowState
            {
                w = RestoreBounds.Width,
                h = RestoreBounds.Height,
                x = RestoreBounds.Left,
                y = RestoreBounds.Top,
                max = WindowState == System.Windows.WindowState.Maximized
            };
            _store.Config.window = w;
            _store.SaveConfig();
            _store.Save();
        }

        // -------------------------------------------------- 快捷键

        void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.N) { New_Click(null, null); e.Handled = true; }
                else if (e.Key == Key.F) { SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; }
                else if (e.Key == Key.E) { Export_Click(null, null); e.Handled = true; }
            }
            else if (e.Key == Key.F5) { Reload(false); e.Handled = true; }
            else if (e.Key == Key.Escape && SearchBox.IsKeyboardFocused) { SearchBox.Clear(); }
        }
    }
}
