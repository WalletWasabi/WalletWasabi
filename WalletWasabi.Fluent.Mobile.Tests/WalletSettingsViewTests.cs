using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class WalletSettingsViewTests
{
	[AvaloniaTheory]
	[InlineData(false, 320)]
	[InlineData(true, 320)]
	[InlineData(false, 390)]
	[InlineData(true, 390)]
	public void NativeSettingsViewsRenderAtPhoneWidths(bool dark, int width)
	{
		foreach (var isCoinjoin in new[] { false, true })
		{
			Control view = isCoinjoin ? new MobileCoinJoinSettingsView() : new MobileWalletSettingsView();
			var window = new Window { Width = width, Height = 844, Content = view, SystemDecorations = SystemDecorations.None,
				RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
			try
			{
				window.Show();
				Dispatcher.UIThread.RunJobs();
				foreach (var scroll in view.GetVisualDescendants().OfType<ScrollViewer>())
				{
					if (scroll.IsVisible && scroll.Viewport.Width > 0)
						Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
				}
				MobileScreenshot.Capture(window, $"settings-unbound-{(isCoinjoin ? "coinjoin" : "wallet")}-{width}-{(dark ? "dark" : "light")}",
					isCoinjoin ? "coinjoin-settings" : "wallet-settings", "unbound-layout");
			}
			finally { window.Close(); }
		}
	}
}
