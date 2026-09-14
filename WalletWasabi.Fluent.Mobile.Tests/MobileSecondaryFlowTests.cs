using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileSecondaryFlowTests
{
	public static IEnumerable<object[]> Cases()
	{
		foreach (var name in new[] { "custom-fee", "receive-addresses", "edit-labels", "hide-address", "payment-sent", "complete", "transaction-details" })
			foreach (var width in new[] { 320, 390 })
				foreach (var dark in new[] { false, true })
					yield return new object[] { name, width, dark };
	}

	/// <summary>Validates view construction and layout, not an end-to-end wallet operation.</summary>
	[AvaloniaTheory]
	[MemberData(nameof(Cases))]
	public void SecondaryFlowsRenderWithoutStartingWalletServices(string name, int width, bool dark)
	{
		Control view = name switch
		{
			"custom-fee" => new MobileCustomFeeView(),
			"receive-addresses" => new MobileReceiveAddressesView(),
			"edit-labels" => new MobileAddressLabelEditView(),
			"hide-address" => new MobileConfirmHideAddressView(),
			"payment-sent" => new MobileSendSuccessView(),
			"complete" => new MobileSuccessView(),
			"transaction-details" => new MobileTransactionDetailsView(),
			_ => throw new ArgumentOutOfRangeException(nameof(name))
		};
		var window = new Window
		{
			Width = width, Height = 844, SystemDecorations = SystemDecorations.None,
			Content = view, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
		};
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			Assert.InRange(view.Bounds.Width, 1, width);
			foreach (var scroll in view.GetVisualDescendants().OfType<ScrollViewer>())
			{
				if (!scroll.IsVisible || scroll.Viewport.Width <= 0) continue;
				Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1,
					$"{name}: content exceeds the {width}-DIP viewport.");
			}
			MobileScreenshot.Capture(window, $"secondary-unbound-{name}-{width}-{(dark ? "dark" : "light")}",
				"secondary-" + name, "unbound-layout");
		}
		finally { window.Close(); }
	}
}
