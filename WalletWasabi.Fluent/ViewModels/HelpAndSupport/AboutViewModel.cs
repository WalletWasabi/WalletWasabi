using System.Collections.Generic;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	IconName = "info_regular",
	Order = 4,
	Category = SearchCategory.HelpAndSupport,
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	IsLocalized = true)]
public partial class AboutViewModel : RoutableViewModel
{
	public AboutViewModel(UiContext uiContext, bool navigateBack = false)
	{
		UiContext = uiContext;

		EnableBack = navigateBack;

		Links = new List<ViewModelBase>()
			{
				new LinkViewModel(UiContext)
				{
					Link = DocsLink,
					Description = Lang.Resources.Words_Documentation,
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel(UiContext)
				{
					Link = SourceCodeLink,
					Description = $"{Lang.Resources.Sentences_SourceCode} ({Lang.Resources.Words_GitHub})",
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel(UiContext)
				{
					Link = ClearnetLink,
					Description = $"{Lang.Resources.Words_Website} ({Lang.Resources.Words_Clearnet})",
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel(UiContext)
				{
					Link = TorLink,
					Description = $"{Lang.Resources.Words_Website} ({Lang.Resources.Words_Tor})",
					IsClickable = false
				},
				new SeparatorViewModel(),
				new LinkViewModel(UiContext)
				{
					Link = StatusPageLink,
					Description = Lang.Resources.Sentences_BackendStatus,
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel(UiContext)
				{
					Link = UserSupportLink,
					Description = Lang.Resources.Sentences_UserSupport,
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel(UiContext)
				{
					Link = BugReportLink,
					Description = Lang.Resources.Sentences_BugReport,
					IsClickable = true
				},
				new SeparatorViewModel(),
				new LinkViewModel(UiContext)
				{
					Link = FAQLink,
					Description = Lang.Resources.Words_FAQ,
					IsClickable = true
				},
			};

		License = new LinkViewModel(UiContext)
		{
			Link = LicenseLink,
			Description = Lang.Resources.Sentences_MITLicense,
			IsClickable = true
		};

		OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(x => UiContext.FileSystem.OpenBrowserAsync(x));

		AboutAdvancedInfoDialogCommand = ReactiveCommand.CreateFromTask(async () => await Navigate().To().AboutAdvancedInfo().GetResultAsync());

		CopyLinkCommand = ReactiveCommand.CreateFromTask<string>(async (link) => await UiContext.Clipboard.SetTextAsync(link));

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

	public static string SourceCodeLink => "https://github.com/WalletWasabi/WalletWasabi/";

	public static string StatusPageLink => "https://stats.uptimerobot.com/pOhAlrGWM9";

	public static string UserSupportLink => "https://github.com/WalletWasabi/WalletWasabi/discussions/5185";

	public static string BugReportLink => "https://github.com/WalletWasabi/WalletWasabi/issues/new?template=bug-report.md";

	public static string FAQLink => "https://docs.wasabiwallet.io/FAQ/";

	public static string DocsLink => "https://docs.wasabiwallet.io/";

	public static string LicenseLink => "https://github.com/WalletWasabi/WalletWasabi/blob/master/LICENSE.md";
}
