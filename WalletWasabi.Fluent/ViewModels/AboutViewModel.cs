using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using JetBrains.Annotations;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.WebClients.Wasabi;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels
{
	public class AboutViewModel : RoutableViewModel
	{
		private string _currentBackendMajorVersion;

		public AboutViewModel(NavigationStateViewModel navigationState)
			: base(navigationState, NavigationTarget.HomeScreen)
		{
			OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(IoHelpers.OpenBrowserAsync);

			CurrentBackendMajorVersion = WasabiClient.ApiVersion.ToString();
			OpenBrowserCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		public ReactiveCommand<string, Unit> OpenBrowserCommand { get; }
		public Version ClientVersion { get; } = Constants.ClientVersion;
		public string BackendCompatibleVersions { get; } = Constants.ClientSupportBackendVersionText;

		public string CurrentBackendMajorVersion
		{
			get => _currentBackendMajorVersion;
			set => this.RaiseAndSetIfChanged(ref _currentBackendMajorVersion, value);
		}

		public Version BitcoinCoreVersion { get; } = Constants.BitcoinCoreVersion;

		public Version HwiVersion { get; } = Constants.HwiVersion;

		public string ClearnetLink { get; } = "https://wasabiwallet.io/";

		public string TorLink { get; } = "http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion";

		public string SourceCodeLink { get; } = "https://github.com/zkSNACKs/WalletWasabi/";

		public string StatusPageLink { get; } = "https://stats.uptimerobot.com/YQqGyUL8A7";

		public string UserSupportLink { get; } = "https://www.reddit.com/r/WasabiWallet/";

		public string BugReportLink { get; } = "https://github.com/zkSNACKs/WalletWasabi/issues/";

		public string FAQLink { get; } = "https://docs.wasabiwallet.io/FAQ/";

		public string DocsLink { get; } = "https://docs.wasabiwallet.io/";
	}
}