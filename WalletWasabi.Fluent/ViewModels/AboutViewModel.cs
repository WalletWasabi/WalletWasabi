using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels
{
	[NavigationMetaData(
		Title = "About Wasabi",
		Caption = "Displays all the current info about the app",
		IconName = "info_regular",
		Order = 4,
		Category = "Help & Support",
		Keywords = new[]
		{
			"About", "Software", "Version", "Source", "Code", "Github", "Status", "Stats", "Tor", "Onion", "Bug",
			"Report", "FAQ", "Questions,", "Docs", "Documentation", "Link", "Links", "Help"
		},
		NavBarPosition = NavBarPosition.None,
		NavigationTarget = NavigationTarget.DialogScreen)]
	public partial class AboutViewModel : RoutableViewModel
	{
		public AboutViewModel()
		{
			OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(IoHelpers.OpenBrowserAsync);

			var interaction = new Interaction<Unit, Unit>();
			interaction.RegisterHandler(
				async x =>
					x.SetOutput((await new AboutAdvancedInfoViewModel().ShowDialogAsync()).Result));

			AboutAdvancedInfoDialogCommand = ReactiveCommand.CreateFromTask(
				execute: async () => await interaction.Handle(Unit.Default).ToTask());

			OpenBrowserCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		public ICommand AboutAdvancedInfoDialogCommand { get; }

		public ReactiveCommand<string, Unit> OpenBrowserCommand { get; }

		public Version ClientVersion => Constants.ClientVersion;

		public static string ClearnetLink => "https://wasabiwallet.io/";

		public static string TorLink => "http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion";

		public static string SourceCodeLink => "https://github.com/zkSNACKs/WalletWasabi/";

		public static string StatusPageLink => "https://stats.uptimerobot.com/YQqGyUL8A7";

		public static string UserSupportLink => "https://www.reddit.com/r/WasabiWallet/";

		public static string BugReportLink => "https://github.com/zkSNACKs/WalletWasabi/issues/";

		public static string FAQLink => "https://docs.wasabiwallet.io/FAQ/";

		public static string DocsLink => "https://docs.wasabiwallet.io/";

		public static string License => "https://github.com/zkSNACKs/WalletWasabi/blob/master/LICENSE.md";
	}
}