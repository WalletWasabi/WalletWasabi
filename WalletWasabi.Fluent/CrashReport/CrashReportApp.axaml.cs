using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.CrashReport.ViewModels;
using WalletWasabi.Models;
using WalletWasabi.Fluent.CrashReport.Views;

namespace WalletWasabi.Fluent.CrashReport;

public class CrashReportApp : Application
{
	private readonly SerializableException? _serializableException;

	public CrashReportApp()
	{
		Name = "Wasabi Wallet Crash Report";
	}

	public CrashReportApp(SerializableException exception) : this()
	{
		_serializableException = exception;
	}

	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && _serializableException is { })
		{
			desktop.MainWindow = new CrashReportWindow
			{
				DataContext = new CrashReportWindowViewModel(_serializableException)
			};
		}

		base.OnFrameworkInitializationCompleted();
	}
}
