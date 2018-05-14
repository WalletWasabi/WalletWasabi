using Avalonia;
using Avalonia.Logging.Serilog;
using WalletWasabi.UI.Desktop.ViewModels;
using WalletWasabi.UI.Desktop.Views;

namespace WalletWasabi.UI.Desktop
{
    class Program
    {
        static void Main(string[] args)
        {
            BuildAvaloniaApp().Start<MainWindow>(() => new MainWindowViewModel());
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .UseReactiveUI()
                .LogToDebug();
    }
}
