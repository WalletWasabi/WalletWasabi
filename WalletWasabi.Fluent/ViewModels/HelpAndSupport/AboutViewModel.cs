using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using Avalonia;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	Title = "About Wasabi",
	Caption = "Displays all the current info about the app",
	IconName = "info_regular",
	Order = 4,
	Category = "Help & Support",
	Keywords = new[]
	{
			"About", "Software", "Version", "Source", "Code", "Github", "Website", "Coordinator", "Status", "Stats", "Tor", "Onion",
			"User", "Support", "Bug", "Report", "FAQ", "Questions,", "Docs", "Documentation", "License", "Advanced", "Information",
			"Hardware", "Wallet"
	},
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class AboutViewModel : RoutableViewModel
{
	public AboutViewModel(bool navigateBack = false)
	{
		EnableBack = navigateBack;

		Links = new List<ViewModelBase>()
			{
				new LinkViewModel()
				{
					Link = DocsLink,
					Description = "Documentation",
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel()
				{
					Link = SourceCodeLink,
					Description = "Source Code (GitHub)",
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel()
				{
					Link = ClearnetLink,
					Description = "Website (Clearnet)",
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel()
				{
					Link = TorLink,
					Description = "Website (Tor)",
					IsClickable = false
				},
				new SeparatorViewModel(),
				new LinkViewModel()
				{
					Link = StatusPageLink,
					Description = "Coordinator Status Page",
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel()
				{
					Link = UserSupportLink,
					Description = "User Support",
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel()
				{
					Link = BugReportLink,
					Description = "Bug Report",
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel()
				{
					Link = FAQLink,
					Description = "FAQ",
					IsClickable = true
				},
			};

		License = new LinkViewModel()
		{
			Link = LicenseLink,
			Description = "MIT License",
			IsClickable = true
		};

		OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(IoHelpers.OpenBrowserAsync);

		AboutAdvancedInfoDialogCommand = ReactiveCommand.CreateFromTask(
			execute: async () => await NavigateDialogAsync(new AboutAdvancedInfoViewModel(), NavigationTarget.CompactDialogScreen));

		OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(
			async (link) =>
				await IoHelpers.OpenBrowserAsync(link));

		CopyLinkCommand = ReactiveCommand.CreateFromTask<string>(
			async (link) =>
				{
					if (Application.Current is { Clipboard: { } clipboard })
					{
						await clipboard.SetTextAsync(link);
					}
				});

		NextCommand = CancelCommand;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public List<ViewModelBase> Links { get; }

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
