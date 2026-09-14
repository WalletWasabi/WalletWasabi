using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(WalletWasabi.Fluent.Mobile.Tests.TestAppBuilder))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace WalletWasabi.Fluent.Mobile.Tests;

public static class TestAppBuilder
{
	public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<MobileTestApplication>()
		.UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

/// <summary>Loads production resources without starting the backend, networking or a wallet.</summary>
public sealed class MobileTestApplication : Application
{
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this, new Uri("avares://WalletWasabi.Fluent/App.axaml"));
	}
}
