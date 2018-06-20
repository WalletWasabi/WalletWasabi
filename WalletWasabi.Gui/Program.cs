using Avalonia;
using Avalonia.Threading;
using System;
using AvalonStudio.Extensibility.Theme;
using AvalonStudio.Shell;

namespace WalletWasabi.Gui
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			BuildAvaloniaApp().AfterSetup(builder =>
			{
			}).StartShellApp("Wasabi Wallet", new DefaultLayoutFactory());
		}

		private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().UseReactiveUI();
	}
}
