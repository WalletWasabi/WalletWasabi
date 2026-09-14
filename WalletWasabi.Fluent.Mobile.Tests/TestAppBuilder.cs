using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(WalletWasabi.Fluent.Mobile.Tests.TestAppBuilder))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace WalletWasabi.Fluent.Mobile.Tests;

public static class TestAppBuilder
{
	public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<MobileTestApplication>()
		.UseSkia()
		.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>Loads production resources without starting the backend, networking or a wallet.</summary>
public sealed class MobileTestApplication : Application
{
	public override void Initialize()
	{
		Styles.Add(new FluentTheme());
		var sources = new[]
		{
			"avares://Avalonia.Controls.TreeDataGrid/Themes/Fluent.axaml",
			"avares://WalletWasabi.Fluent/Styles/Themes/Fluent.axaml",
			"avares://WalletWasabi.Fluent/Icons/Icons.axaml",
			"avares://WalletWasabi.Fluent/Styles/Styles.axaml",
			"avares://WalletWasabi.Fluent/Mobile/Styles/MobileTheme.axaml",
			"avares://WalletWasabi.Fluent/Mobile/Styles/MobileControls.axaml",
			"avares://WalletWasabi.Fluent/Mobile/Styles/MobileCompositionTokens.axaml",
			"avares://WalletWasabi.Fluent/Mobile/Styles/MobileEntryControls.axaml"
		};
		foreach (var source in sources)
			Styles.Add(new StyleInclude(new Uri("avares://WalletWasabi.Fluent/")) { Source = new Uri(source) });
		Resources["ToggleSwitchThemeMinWidth"] = 0d;
		DataTemplates.Add(new ViewLocator());
	}
}
