using System.IO;
using ReactiveUI;
using System.Windows.Input;
using Avalonia;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.CrashReport.ViewModels;

public class CrashReportWindowViewModel : ViewModelBase
{
	public CrashReportWindowViewModel(SerializableException serializedException)
	{
		SerializedException = serializedException;
		CancelCommand = ReactiveCommand.Create(AppLifetimeHelper.Restart);
		NextCommand = ReactiveCommand.Create(AppLifetimeHelper.Shutdown);

		OpenGitHubRepoCommand = ReactiveCommand.CreateFromTask(async () => await IoHelpers.OpenBrowserAsync(AboutViewModel.UserSupportLink));

		CopyTraceCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (Application.Current is { Clipboard: { } clipboard })
			{
				await clipboard.SetTextAsync(Trace);
			}
		});
	}

	public SerializableException SerializedException { get; }

	public ICommand OpenGitHubRepoCommand { get; }

	public ICommand NextCommand { get; }

	public ICommand CancelCommand { get; }

	public ICommand CopyTraceCommand { get; }

	public string Caption => $"A problem has occurred and Wasabi is unable to continue.";

	public string Trace => $"{SerializedException.Message}{Environment.NewLine}" +
						   $"{Environment.NewLine}{SerializedException.StackTrace}";

	public string Title => "Wasabi has crashed";
}
