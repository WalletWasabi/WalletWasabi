using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.Settings;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileShellNavigationTests
{
	[Theory]
	[InlineData(false, false, false, MobileShellLayer.Main)]
	[InlineData(true, false, false, MobileShellLayer.FullScreen)]
	[InlineData(false, true, false, MobileShellLayer.Dialog)]
	[InlineData(true, true, false, MobileShellLayer.Dialog)]
	[InlineData(false, false, true, MobileShellLayer.CompactDialog)]
	[InlineData(true, false, true, MobileShellLayer.CompactDialog)]
	[InlineData(false, true, true, MobileShellLayer.CompactDialog)]
	[InlineData(true, true, true, MobileShellLayer.CompactDialog)]
	public void DisplayAndBackChooseTheSameTopmostStack(bool full, bool dialog, bool compact, MobileShellLayer expected)
	{
		Assert.Equal(expected, MobileShellNavigation.GetTopLayer(full, dialog, compact));
	}

	public static IEnumerable<object?[]> NavigationCases()
	{
		yield return [typeof(WelcomePageViewModel), true];
		yield return [typeof(AddWalletPageViewModel), true];
		yield return [typeof(MobileWalletsListViewModel), true];
		yield return [typeof(LoginViewModel), true];
		yield return [typeof(LoadingViewModel), true];
		yield return [typeof(SettingsPageViewModel), true];
		yield return [typeof(WalletViewModel), false];
		yield return [typeof(WalletNamePageViewModel), false];
		yield return [typeof(SendViewModel), false];
		yield return [typeof(TransactionPreviewViewModel), false];
		yield return [typeof(PasswordAuthDialogViewModel), false];
		yield return [typeof(HardwareWalletAuthDialogViewModel), false];
		yield return [typeof(object), false];
		yield return [null, false];
	}

	[Theory]
	[MemberData(nameof(NavigationCases))]
	public void GlobalNavigationIsLimitedToKnownLandingPages(Type? page, bool expected)
	{
		// Type-only test: does not construct a wallet, services or an authorization request.
		Assert.Equal(expected, MobileShellNavigation.ShowApplicationNavigation(page));
	}
}
