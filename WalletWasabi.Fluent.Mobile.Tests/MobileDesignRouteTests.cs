using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

/// <summary>Whole-route layout evidence complements populated send/receive/wallet and preference-control tests.</summary>
public sealed class MobileDesignRouteTests
{
	public static IEnumerable<object[]> Cases()
	{
		foreach (var route in new[] { "settings", "review", "fee" })
			foreach (var width in new[] { 320, 390, 430 })
				foreach (var dark in new[] { false, true }) yield return new object[] { route, width, dark };
	}
	[AvaloniaTheory]
	[MemberData(nameof(Cases))]
	public void WholeDesignRoutesRenderInBothThemes(string route, int width, bool dark)
	{
		Control view = route switch
		{
			"settings" => new MobileSettingsView(),
			"review" => new MobileTransactionPreviewView(),
			"fee" => new MobileSendFeeView(),
			_ => throw new ArgumentOutOfRangeException(nameof(route))
		};
		var window = new Window { Width = width, Height = 844, Content = view, SystemDecorations = SystemDecorations.None,
			RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
		try
		{
			window.Show();
			MobileScreenshot.Capture(window, $"design-{route}-{width}-{(dark ? "dark" : "light")}", route, "unbound-layout");
			MobileScreenshot.AssertNoHorizontalOverflow(window);
		}
		finally { window.Close(); }
	}
}
