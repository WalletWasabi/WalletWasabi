using ReactiveUI;
using System.Windows.Input;
using Avalonia;
using CommunityToolkit.Mvvm.Input;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.Models;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.CrashReport.ViewModels;

public class CrashReportWindowViewModel : ViewModelBase
{
	public CrashReportWindowViewModel(SerializableException serializedException)
	{
		SerializedException = serializedException;
		CancelCommand = new RelayCommand(() => AppLifetimeHelper.Shutdown(withShutdownPrevention: false, restart: true));
		NextCommand = new RelayCommand(() => AppLifetimeHelper.Shutdown(withShutdownPrevention: false, restart: false));

		OpenGitHubRepoCommand = new AsyncRelayCommand(async () => await IoHelpers.OpenBrowserAsync(AboutViewModel.UserSupportLink));

		CopyTraceCommand = new AsyncRelayCommand(async () =>
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

	public string Trace => SerializedException.ToString();

	public string Title => "Wasabi has crashed";
}
