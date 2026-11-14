using System;
using System.Windows;
using System.Windows.Threading;

namespace TripBook
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 「发现新版本」检查：命令行开关 + 无人值守 probe
            UpdateKit.UpdateCheck.Configure(e.Args);
            var probeExit = UpdateKit.UpdateCheck.TryProbe(e.Args);
            if (probeExit.HasValue)
            {
                Shutdown(probeExit.Value);
                return;
            }

            DispatcherUnhandledException += OnCrash;
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                if (ex != null) ShowError(ex);
            };

            // 启动后检查是否有新版本（发现则弹窗提示，可跳过该版本）
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                UpdateKit.UpdatePrompt.ScheduleAutoCheck(Current.MainWindow);
            }));
        }

        void OnCrash(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ShowError(e.Exception);
            e.Handled = true;
        }

        static void ShowError(Exception ex)
        {
            try
            {
                MessageBox.Show("程序遇到问题：\n\n" + ex.Message + "\n\n" + ex.StackTrace,
                    "行程簿 TripBook", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        }
    }
}
