using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WalletWasabi.Gui.CrashReporter.ViewModels;
using WalletWasabi.Gui.CrashReporter.Views;

namespace WalletWasabi.Gui.CrashReporter
{
	public class CrashReportApp : Application
	{
		public CrashReportApp()
		{
			Name = "Wasabi Wallet Crash Reporting";
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
