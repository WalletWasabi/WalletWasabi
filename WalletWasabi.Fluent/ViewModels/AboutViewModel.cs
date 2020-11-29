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
		public AboutViewModel(NavigationStateViewModel navigationState) : base(navigationState,
			NavigationTarget.HomeScreen)
		{
			OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(IoHelpers.OpenBrowserAsync);

			OpenBrowserCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		public ReactiveCommand<string, Unit> OpenBrowserCommand { get; }
		public Version ClientVersion => Constants.ClientVersion;
		public Version BitcoinCoreVersion => Constants.BitcoinCoreVersion;
		public Version HwiVersion => Constants.HwiVersion;
		public string BackendCompatibleVersions => Constants.ClientSupportBackendVersionText;
		public string CurrentBackendMajorVersion => WasabiClient.ApiVersion.ToString();
		public string ClearnetLink => "https://wasabiwallet.io/";
		public string TorLink => "http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion";
		public string SourceCodeLink => "https://github.com/zkSNACKs/WalletWasabi/";
		public string StatusPageLink => "https://stats.uptimerobot.com/YQqGyUL8A7";
		public string UserSupportLink => "https://www.reddit.com/r/WasabiWallet/";
		public string BugReportLink => "https://github.com/zkSNACKs/WalletWasabi/issues/";
		public string FAQLink => "https://docs.wasabiwallet.io/FAQ/";
		public string DocsLink => "https://docs.wasabiwallet.io/";
	}
}