using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace TripBook
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            var app = new Application();
            var host = new Window
            {
                Width = 800, Height = 500,
                Background = Palette.Brush("#0D1015"),
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Title = "host"
            };
            host.Show();

            bool result = false;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                var dlg = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w != host);
                if (dlg != null && dlg.ActualWidth > 0)
                {
                    var rtb = new RenderTargetBitmap((int)dlg.ActualWidth, (int)dlg.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(dlg);
                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(rtb));
                    using (var fs = File.Create("dialog.png")) enc.Save(fs);
                    Console.WriteLine("captured " + dlg.ActualWidth + "x" + dlg.ActualHeight);
                    dlg.Close();
                }
                else Console.WriteLine("dialog not found");
                host.Close();
                app.Shutdown();
            };
            timer.Start();

            result = Dialogs.Confirm(host, "删除行程",
                "确定删除「12.6 行程」？\n此操作不可撤销。", "删除", true);

            Console.WriteLine("result=" + result);
        }
    }
}
