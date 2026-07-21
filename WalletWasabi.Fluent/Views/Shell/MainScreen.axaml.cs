using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.Views.Shell;

public class MainScreen : UserControl
{
	public MainScreen()
	{
		InitializeComponent();
		this.GetObservable(BoundsProperty).Subscribe(bounds =>
		{
			WalletWasabi.Logging.Logger.LogInfo($"[MobileUI] MainScreen Bounds: {bounds.Width}x{bounds.Height}");
			bool isMobile = bounds.Width > 0 && bounds.Width <= 600;
			if (DataContext is MainViewModel mainVm)
			{
				mainVm.IsMobileLayout = isMobile;
			}
			else if (DataContext is WalletWasabi.Fluent.ViewModels.ApplicationViewModel appVm)
			{
				appVm.MainViewModel.IsMobileLayout = isMobile;
			}
			if (isMobile)
			{
				if (!Classes.Contains("mobile"))
				{
					Classes.Add("mobile");
				}
			}
			else
			{
				Classes.Remove("mobile");
			}

			var sidebar = this.FindControl<Control>("SidebarNavBar");
			var titlebar = this.FindControl<Control>("MainTitleBar");
			var contentPart = this.FindControl<Border>("ContentPart");
			var mobileNav = this.FindControl<Control>("MobileShellNav");

			if (sidebar != null)
			{
				sidebar.IsVisible = !isMobile;
			}
			if (titlebar != null)
			{
				titlebar.IsVisible = !isMobile;
			}
			if (contentPart != null)
			{
				contentPart.CornerRadius = isMobile ? new CornerRadius(0) : new CornerRadius(10, 0, 0, 0);
			}

			var floatingStatus = this.FindControl<Control>("FloatingStatusIcon");
			WalletWasabi.Logging.Logger.LogInfo($"[MobileUI] FloatingStatusIcon found: {floatingStatus != null}, isMobile: {isMobile}");
			if (floatingStatus != null)
			{
				floatingStatus.IsVisible = !isMobile;
			}

			if (!Classes.Contains("ios") && !Classes.Contains("android"))
			{
				if (OperatingSystem.IsIOS())
				{
					Classes.Add("ios");
				}
				else if (OperatingSystem.IsAndroid())
				{
					Classes.Add("android");
				}
				else
				{
					Classes.Add("ios"); // Fallback for macOS simulator verification
				}
			}

			if (mobileNav != null)
			{
				if (!mobileNav.Classes.Contains("ios") && !mobileNav.Classes.Contains("android"))
				{
					if (OperatingSystem.IsIOS())
					{
						mobileNav.Classes.Add("ios");
					}
					else if (OperatingSystem.IsAndroid())
					{
						mobileNav.Classes.Add("android");
					}
					else
					{
						mobileNav.Classes.Add("ios"); // Fallback for macOS simulator verification
					}
				}
			}
		});

		this.GetObservable(DataContextProperty).Subscribe(dc =>
		{
			if (dc is MainViewModel vm)
			{
				vm.WhenAnyValue(x => x.ActiveMobileTab)
					.Subscribe(tab => UpdateMobileTabSelection(tab));
			}
		});
	}

	private void UpdateMobileTabSelection(string tab)
	{
		var walletsBtn = this.FindControl<Button>("WalletsTabButton");
		var addWalletBtn = this.FindControl<Button>("AddWalletTabButton");
		var settingsBtn = this.FindControl<Button>("SettingsTabButton");

		if (walletsBtn != null) walletsBtn.Classes.Remove("active");
		if (addWalletBtn != null) addWalletBtn.Classes.Remove("active");
		if (settingsBtn != null) settingsBtn.Classes.Remove("active");

		var walletsIcon = this.FindControl<PathIcon>("WalletsTabIcon");
		if (walletsIcon != null)
		{
			walletsIcon.Data = tab == "Wallets"
				? (this.FindResource("nav_wallet_24_filled") as Avalonia.Media.Geometry)!
				: (this.FindResource("nav_wallet_24_regular") as Avalonia.Media.Geometry)!;
		}

		if (tab == "Wallets" && walletsBtn != null) walletsBtn.Classes.Add("active");
		if (tab == "AddWallet" && addWalletBtn != null) addWalletBtn.Classes.Add("active");
		if (tab == "Settings" && settingsBtn != null) settingsBtn.Classes.Add("active");
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
