using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;

namespace WalletWasabi.Fluent.Mobile.Views;

public abstract class MobileSettingsPanel : UserControl
{
	protected MobileSettingsPanel() => Styles.Add(new StyleInclude(new Uri("avares://WalletWasabi.Fluent/"))
	{
		Source = new Uri("avares://WalletWasabi.Fluent/Mobile/Styles/MobileSettingsControls.axaml")
	});
}

public sealed class MobileGeneralSettingsPanel : MobileSettingsPanel
{
	public MobileGeneralSettingsPanel() => AvaloniaXamlLoader.Load(this);
}
public sealed class MobileBitcoinSettingsPanel : MobileSettingsPanel
{
	public MobileBitcoinSettingsPanel() => AvaloniaXamlLoader.Load(this);
}
public sealed class MobileCoordinatorSettingsPanel : MobileSettingsPanel
{
	public MobileCoordinatorSettingsPanel() => AvaloniaXamlLoader.Load(this);
}
public sealed class MobileConnectionsSettingsPanel : MobileSettingsPanel
{
	public MobileConnectionsSettingsPanel() => AvaloniaXamlLoader.Load(this);
}

/// <summary>Desktop-only settings are not advertised as implemented mobile operating-system integrations.</summary>
public static class MobileSettingsPlatform
{
	public static bool ShowDesktopOptions => !OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS() &&
		Application.Current?.ApplicationLifetime is not ISingleViewApplicationLifetime &&
		Environment.GetEnvironmentVariable("WASABI_MOBILE_REFERENCE") != "1";
}
