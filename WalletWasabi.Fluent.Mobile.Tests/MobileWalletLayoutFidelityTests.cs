using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileWalletLayoutFidelityTests
{
	[AvaloniaTheory]
	[InlineData(320)]
	[InlineData(390)]
	[InlineData(430)]
	public void LiveThemeChangeUsesReferenceHomeAndPrivacyCompositions(int width)
	{
		var fixture = new MobileWalletFixture();
		var surface = new MobileWalletSurface { DataContext = fixture };
		var window = new Window { Width = width, Height = 932, Content = surface, SystemDecorations = SystemDecorations.None };
		try
		{
			window.Show();
			foreach (var dark in new[] { false, true, false })
			{
				window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
				fixture.Navigate("home"); Dispatcher.UIThread.RunJobs();
				var card = surface.FindControl<Border>("BalanceCard")!;
				Assert.Equal(dark ? 172d : 190d, card.Bounds.Height);
				Assert.Equal(dark, surface.FindControl<Border>("HomePrivacyQuote")!.IsVisible);
				var amount = surface.FindControl<TextBlock>("WalletBalance")!;
				Assert.True(amount.Bounds.Width >= card.Bounds.Width - 34, "The chart must not squeeze the amount into a narrow column.");
				fixture.Navigate("privacy"); Dispatcher.UIThread.RunJobs();
				var gauge = surface.FindControl<Grid>("PrivacyGaugeContainer")!;
				Assert.Equal(dark ? 208d : 156d, gauge.Bounds.Width);
				Assert.Equal(dark, surface.FindControl<Border>("DarkPrivacyMetrics")!.IsVisible);
				Assert.Equal(!dark, surface.FindControl<Control>("LightPrivacyMetrics")!.IsVisible);
				var legend = surface.FindControl<StackPanel>("PrivacyLegend")!;
				Assert.Equal(!dark, legend.IsVisible);
				if (!dark) Assert.True(gauge.Bounds.Right + 8 <= legend.Bounds.X, "The light gauge and legend must not overlap.");
				MobileScreenshot.AssertNoHorizontalOverflow(surface);
			}
		}
		finally { window.Close(); }
	}
}
