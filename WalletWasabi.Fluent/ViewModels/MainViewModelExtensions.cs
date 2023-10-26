using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.Fluent.ViewModels.OpenDirectory;
using WalletWasabi.Fluent.ViewModels.Settings;
using WalletWasabi.Fluent.ViewModels.TransactionBroadcasting;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

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

		AdvancedSettingsTabViewModel.RegisterLazy(() =>
		{
			mainViewModel.SettingsPage.SelectedTab = 2;
			return mainViewModel.SettingsPage;
		});

		AboutViewModel.RegisterLazy(() => new AboutViewModel(uiContext));
		BroadcasterViewModel.RegisterLazy(() => new BroadcasterViewModel(uiContext));
		LegalDocumentsViewModel.RegisterLazy(() => new LegalDocumentsViewModel(uiContext));
		UserSupportViewModel.RegisterLazy(() => new UserSupportViewModel(uiContext));
		BugReportLinkViewModel.RegisterLazy(() => new BugReportLinkViewModel(uiContext));
		DocsLinkViewModel.RegisterLazy(() => new DocsLinkViewModel(uiContext));
		OpenDataFolderViewModel.RegisterLazy(() => new OpenDataFolderViewModel(uiContext));
		OpenWalletsFolderViewModel.RegisterLazy(() => new OpenWalletsFolderViewModel(uiContext));
		OpenLogsViewModel.RegisterLazy(() => new OpenLogsViewModel(uiContext));
		OpenTorLogsViewModel.RegisterLazy(() => new OpenTorLogsViewModel(uiContext));
		OpenConfigFileViewModel.RegisterLazy(() => new OpenConfigFileViewModel(uiContext));

		WalletCoinsViewModel.RegisterLazy(() =>
		{
			if (mainViewModel.NavBar.SelectedWalletModel is { } wallet)
			{
				return new WalletCoinsViewModel(uiContext, wallet);
			}

			return null;
		});

		CoinJoinSettingsViewModel.RegisterLazy(() =>
		{
			if (mainViewModel.NavBar.SelectedWallet?.WalletViewModel is { } walletViewModel && !walletViewModel.IsWatchOnly)
			{
				return walletViewModel.CoinJoinSettings;
			}

			return null;
		});

		WalletSettingsViewModel.RegisterLazy(() =>
		{
			if (mainViewModel.NavBar.SelectedWallet?.WalletViewModel is { } walletViewModel)
			{
				return walletViewModel.Settings;
			}

			return null;
		});

		WalletStatsViewModel.RegisterLazy(() =>
		{
			if (mainViewModel.NavBar.SelectedWalletModel is { } wallet)
			{
				return new WalletStatsViewModel(uiContext, wallet);
			}

			return null;
		});

		WalletInfoViewModel.RegisterAsyncLazy(async () =>
		{
			if (mainViewModel.NavBar.SelectedWalletModel is { } wallet)
			{
				if (wallet.Auth.HasPassword)
				{
					var authenticated = await uiContext.Navigate().To().PasswordAuthDialog(wallet).GetResultAsync();
					if (authenticated)
					{
						return new WalletInfoViewModel(uiContext, wallet);
					}
				}
			}

			return null;
		});

		SendViewModel.RegisterLazy(() =>
		{
			if (mainViewModel.NavBar.SelectedWallet?.WalletViewModel is { } walletViewModel)
			{
				// TODO: Check if we can send?
				return new SendViewModel(uiContext, walletViewModel);
			}

			return null;
		});

		ReceiveViewModel.RegisterLazy(() =>
		{
			if (mainViewModel.NavBar.SelectedWalletModel is { } wallet)
			{
				return new ReceiveViewModel(uiContext, wallet);
			}

			return null;
		});
	}
}
