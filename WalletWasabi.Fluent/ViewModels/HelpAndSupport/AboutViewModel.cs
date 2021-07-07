using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
			Links = new List<LinkViewModel>()
			{
				new ()
				{
					Link = DocsLink,
					Description = "Documentation",
					IsClickable = true,
					IsLast = false
				},
				new ()
				{
					Link = SourceCodeLink,
					Description = "Source Code (Github)",
					IsClickable = true,
					IsLast = false
				},
				new ()
				{
					Link = ClearnetLink,
					Description = "Website (Clearnet)",
					IsClickable = true,
					IsLast = false
				},
				new ()
				{
					Link = TorLink,
					Description = "Website (Tor)",
					IsClickable = false,
					IsLast = false
				},
				new ()
				{
					Link = StatusPageLink,
					Description = "Coordinator Status Page",
					IsClickable = true,
					IsLast = false
				},
				new ()
				{
					Link = UserSupportLink,
					Description = "User Support",
					IsClickable = true,
					IsLast = false
				},
				new ()
				{
					Link = BugReportLink,
					Description = "Bug Reporting",
					IsClickable = true,
					IsLast = false
				},
				new ()
				{
					Link = FAQLink,
					Description = "FAQs",
					IsClickable = true,
					IsLast = true
				},
			};

			License = new()
			{
				Link = LicenseLink,
				Description = "MIT License",
				IsClickable = true,
				IsLast = true
			};

			OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(IoHelpers.OpenBrowserAsync);

			AboutAdvancedInfoDialogCommand = ReactiveCommand.CreateFromTask(
				execute: async () => await NavigateDialogAsync(new AboutAdvancedInfoViewModel(), NavigationTarget.CompactDialogScreen));

			OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(
				async (link) =>
					await IoHelpers.OpenBrowserAsync(link));

			CopyLinkCommand = ReactiveCommand.CreateFromTask<string>(
				async (link) =>
					await Application.Current.Clipboard.SetTextAsync(link));

			NextCommand = CancelCommand;

			SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		}

		public List<LinkViewModel> Links { get; }

		public LinkViewModel License { get; }

		public ICommand AboutAdvancedInfoDialogCommand { get; }

		public ICommand OpenBrowserCommand { get; }

		public ICommand CopyLinkCommand { get; }

		public Version ClientVersion => Constants.ClientVersion;

		public static string ClearnetLink => "https://wasabiwallet.io/";

		public static string TorLink => "http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion";

		public static string SourceCodeLink => "https://github.com/zkSNACKs/WalletWasabi/";

		public static string StatusPageLink => "https://stats.uptimerobot.com/YQqGyUL8A7";

		public static string UserSupportLink => "https://github.com/zkSNACKs/WalletWasabi/discussions/5185";

		public static string BugReportLink => "https://github.com/zkSNACKs/WalletWasabi/issues/";

		public static string FAQLink => "https://docs.wasabiwallet.io/FAQ/";

		public static string DocsLink => "https://docs.wasabiwallet.io/";

		public static string LicenseLink => "https://github.com/zkSNACKs/WalletWasabi/blob/master/LICENSE.md";
	}
}
