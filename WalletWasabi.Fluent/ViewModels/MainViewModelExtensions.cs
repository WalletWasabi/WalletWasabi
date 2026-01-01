using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.Fluent.ViewModels.OpenDirectory;
using WalletWasabi.Fluent.ViewModels.Scheme;
using WalletWasabi.Fluent.ViewModels.Settings;
using WalletWasabi.Fluent.ViewModels.TransactionBroadcasting;

namespace WalletWasabi.Fluent.ViewModels;

public static class MainViewModelExtensions
{
	public static void RegisterAllViewModels(this MainViewModel mainViewModel, UiContext uiContext)
	{
		PrivacyModeViewModel.Register(mainViewModel.PrivacyMode);
		AddWalletPageViewModel.RegisterLazy(() => new AddWalletPageViewModel(uiContext));
		SettingsPageViewModel.Register(mainViewModel.SettingsPage);

		GeneralSettingsTabViewModel.RegisterLazy(() =>
		{
			mainViewModel.SettingsPage.SelectedTab = 0;
			return mainViewModel.SettingsPage;
		});

		BitcoinTabSettingsViewModel.RegisterLazy(() =>
		{
			mainViewModel.SettingsPage.SelectedTab = 1;
			return mainViewModel.SettingsPage;
		});

		CoordinatorTabSettingsViewModel.RegisterLazy(() =>
		{
			mainViewModel.SettingsPage.SelectedTab = 2;
			return mainViewModel.SettingsPage;
		});


		ConnectionsSettingsTabViewModel.RegisterLazy(() =>
		{
			mainViewModel.SettingsPage.SelectedTab = 3;
			return mainViewModel.SettingsPage;
		});

		AboutViewModel.RegisterLazy(() => new AboutViewModel(uiContext));
		BroadcasterViewModel.RegisterLazy(() => new BroadcasterViewModel(uiContext));
		UserSupportViewModel.RegisterLazy(() => new UserSupportViewModel(uiContext));
		BugReportLinkViewModel.RegisterLazy(() => new BugReportLinkViewModel(uiContext));
		DocsLinkViewModel.RegisterLazy(() => new DocsLinkViewModel(uiContext));
		OpenDataFolderViewModel.RegisterLazy(() => new OpenDataFolderViewModel(uiContext));
		FindCoordinatorLinkViewModel.RegisterLazy(() => new FindCoordinatorLinkViewModel(uiContext));
		OpenWalletsFolderViewModel.RegisterLazy(() => new OpenWalletsFolderViewModel(uiContext));
		OpenLogsViewModel.RegisterLazy(() => new OpenLogsViewModel(uiContext));
		OpenTorLogsViewModel.RegisterLazy(() => new OpenTorLogsViewModel(uiContext));
		OpenConfigFileViewModel.RegisterLazy(() => new OpenConfigFileViewModel(uiContext));
		if (uiContext.ApplicationSettings.ExperimentalFeatures.Contains("scripting",
			    StringComparer.InvariantCultureIgnoreCase))
		{
			SchemeConsoleViewModel.Register(new SchemeConsoleViewModel(uiContext.Scheme));
		}
	}
}
