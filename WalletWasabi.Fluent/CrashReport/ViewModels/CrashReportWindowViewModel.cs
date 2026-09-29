using ReactiveUI;
using System.Windows.Input;
using WalletWasabi.Client.Configuration;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.Models;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.CrashReport.ViewModels;

public class CrashReportWindowViewModel : ViewModelBase
{
	public CrashReportWindowViewModel(UiContext uiContext, SerializableException serializedException) : base(uiContext)
	{
		SerializedException = serializedException;
		IsUnreadableConfig = serializedException.ExceptionType == typeof(UnreadableConfigException).FullName;
		CancelCommand = IsUnreadableConfig
			? ReactiveCommand.Create(StartWithDefaultConfig)
			: ReactiveCommand.Create(() => AppLifetimeHelper.Shutdown(withShutdownPrevention: false, restart: true));
		NextCommand = ReactiveCommand.Create(() => AppLifetimeHelper.Shutdown(withShutdownPrevention: false, restart: false));

		OpenGitHubRepoCommand = ReactiveCommand.CreateFromTask(async () => await IoHelpers.OpenBrowserAsync(Link));

		CopyTraceCommand = ReactiveCommand.CreateFromTask(async () => await ApplicationHelper.SetTextAsync(Trace));
	}

	public SerializableException SerializedException { get; }

	/// <summary>Not a crash: a config file can't be read. The user chooses between fixing it (Close) and the default settings.</summary>
	public bool IsUnreadableConfig { get; }

	public ICommand OpenGitHubRepoCommand { get; }

	public ICommand NextCommand { get; }

	public ICommand CancelCommand { get; }

	public ICommand CopyTraceCommand { get; }

	public string Caption => IsUnreadableConfig
		? "Close Wasabi to fix the file yourself, or start with the default settings. The file is then moved aside, not overwritten."
		: "A problem has occurred and Wasabi is unable to continue.";

	public string CancelContent => IsUnreadableConfig ? "Use default settings" : "Restart Wasabi";

	public string Link => AboutViewModel.BugReportLink;

	public string Trace => IsUnreadableConfig ? SerializedException.Message : SerializedException.ToString();

	public string Title => IsUnreadableConfig ? "Wasabi can't read its settings" : "Wasabi has crashed";

	private static void StartWithDefaultConfig()
	{
		AppLifetimeHelper.StartAppWithArgs([.. CrashReporter.GetOriginalArgs(), PersistentConfigManager.ResetUnreadableConfigArgument]);
		AppLifetimeHelper.Shutdown(withShutdownPrevention: false, restart: false);
	}
}
