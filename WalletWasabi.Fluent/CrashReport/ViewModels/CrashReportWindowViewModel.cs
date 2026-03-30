using ReactiveUI;
using System.Windows.Input;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets.FilterProcessor;

namespace WalletWasabi.Fluent.CrashReport.ViewModels;

public class CrashReportWindowViewModel : ViewModelBase
{
	public CrashReportWindowViewModel(SerializableException serializedException)
	{
		SerializedException = serializedException;
		IsWalletRecovery = serializedException.ExceptionType == typeof(WalletFilterNotFoundException).FullName;
		CancelCommand = ReactiveCommand.Create(() => AppLifetimeHelper.Shutdown(withShutdownPrevention: false, restart: true));
		NextCommand = ReactiveCommand.Create(() => AppLifetimeHelper.Shutdown(withShutdownPrevention: false, restart: false));

		OpenGitHubRepoCommand = ReactiveCommand.CreateFromTask(async () => await IoHelpers.OpenBrowserAsync(Link));

		CopyTraceCommand = ReactiveCommand.CreateFromTask(async () => await ApplicationHelper.SetTextAsync(Trace));
	}

	public SerializableException SerializedException { get; }

	public bool IsWalletRecovery { get; }

	public ICommand OpenGitHubRepoCommand { get; }

	public ICommand NextCommand { get; }

	public ICommand CancelCommand { get; }

	public ICommand CopyTraceCommand { get; }

	public string Caption => IsWalletRecovery
		? "Wallet recovery is in progress. Please restart Wasabi to continue syncing your wallet."
		: "A problem has occurred and Wasabi is unable to continue.";

	public string Link => AboutViewModel.BugReportLink;

	public string Trace => IsWalletRecovery
		? "During wallet recovery, Wasabi needs to re-download block filters from the beginning. This requires a one-time restart.\n\nAfter restarting, your wallet will continue syncing automatically. No further action is needed."
		: SerializedException.ToString();

	public string Title => IsWalletRecovery
		? "Wasabi needs to restart"
		: "Wasabi has crashed";
}
