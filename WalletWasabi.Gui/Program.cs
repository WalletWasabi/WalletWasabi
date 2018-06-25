using Avalonia;
using AvalonStudio.Shell;
using System;
using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui
{
	internal class Program
	{
#pragma warning disable IDE1006 // Naming Styles

		private static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			StatusBarViewModel statusBar = null;
			try
			{
				statusBar = new StatusBarViewModel();

				MainWindowViewModel.Instance = new MainWindowViewModel(statusBar);

				BuildAvaloniaApp()
					.StartShellApp<AppBuilder, MainWindow>("Wasabi Wallet", new DefaultLayoutFactory(), () => MainWindowViewModel.Instance);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}
			finally
			{
				statusBar?.Dispose();
			}
		}

		private static AppBuilder BuildAvaloniaApp()
		{
			return AppBuilder.Configure<App>().UsePlatformDetect().UseReactiveUI();
		}
	}
}
