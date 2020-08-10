using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WalletWasabi.Gui.CrashReport.ViewModels;
using WalletWasabi.Gui.CrashReport.Views;

namespace WalletWasabi.Gui.CrashReport
{
	public class CrashReportApp : Application
	{
		public CrashReportApp()
		{
			Name = "WasabiWallet Crash Report";
		}

		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
		}

		public override void OnFrameworkInitializationCompleted()
		{
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				desktop.MainWindow = new CrashReportWindow()
				{
					DataContext = new CrashReportWindowViewModel()
				};
			}

			base.OnFrameworkInitializationCompleted();
		}
	}
}
