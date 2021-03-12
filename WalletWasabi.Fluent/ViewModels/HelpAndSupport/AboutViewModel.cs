using System;
using System.IO;
using System.Windows.Input;
using Avalonia;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport
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

			AboutAdvancedInfoDialogCommand = ReactiveCommand.CreateFromTask(
				execute: async () => await NavigateDialog(new AboutAdvancedInfoViewModel()));

			OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(
				async (link) =>
					await IoHelpers.OpenBrowserAsync(link));

			CopyLinkCommand = ReactiveCommand.CreateFromTask<string>(
				async (link) =>
					await Application.Current.Clipboard.SetTextAsync(link));

			NextCommand = CancelCommand;
		}

		public ICommand AboutAdvancedInfoDialogCommand { get; }

		public ICommand OpenBrowserCommand { get; }

		public ICommand CopyLinkCommand { get; }

		public Version ClientVersion => Constants.ClientVersion;

		public static string ClearnetLink => "https://wasabiwallet.io/";

		public static string TorLink => "http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion";

		public static string SourceCodeLink => "https://github.com/zkSNACKs/WalletWasabi/";

		public static string StatusPageLink => "https://stats.uptimerobot.com/YQqGyUL8A7";

		public static string UserSupportLink => "https://www.reddit.com/r/WasabiWallet/";

		public static string BugReportLink => "https://github.com/zkSNACKs/WalletWasabi/issues/";

		public static string FAQLink => "https://docs.wasabiwallet.io/FAQ/";

		public static string DocsLink => "https://docs.wasabiwallet.io/";

		public static string LicenseLink => "https://github.com/zkSNACKs/WalletWasabi/blob/master/LICENSE.md";
	}
}
