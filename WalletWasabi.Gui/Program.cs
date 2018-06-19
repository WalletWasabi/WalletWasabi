using Avalonia;
using Avalonia.Threading;
using System;

namespace WalletWasabi.Gui
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			AppBuilder.Configure<App>()
				.UsePlatformDetect()
				.AfterSetup(builder =>
				{
				})
				.Start<MainWindow>();
		}

		private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
	}
}
