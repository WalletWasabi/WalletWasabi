using System;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.Settings;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.Mobile.ViewModels;

public enum MobileShellLayer
{
	Main,
	FullScreen,
	Dialog,
	CompactDialog
}

/// <summary>Shared decisions for display, keyboard focus and Back routing. Unknown pages fail closed.</summary>
public static class MobileShellNavigation
{
	public static MobileShellLayer GetTopLayer(bool hasFullScreenPage, bool hasDialogPage, bool hasCompactPage) =>
		hasCompactPage ? MobileShellLayer.CompactDialog :
		hasDialogPage ? MobileShellLayer.Dialog :
		hasFullScreenPage ? MobileShellLayer.FullScreen : MobileShellLayer.Main;

	public static bool ShowApplicationNavigation(Type? pageType) =>
		pageType == typeof(WelcomePageViewModel) ||
		pageType == typeof(AddWalletPageViewModel) ||
		pageType == typeof(MobileWalletsListViewModel) ||
		pageType == typeof(LoginViewModel) ||
		pageType == typeof(LoadingViewModel) ||
		pageType == typeof(SettingsPageViewModel);
}
