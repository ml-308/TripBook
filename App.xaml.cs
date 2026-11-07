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
            DispatcherUnhandledException += OnCrash;
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                if (ex != null) ShowError(ex);
            };
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
