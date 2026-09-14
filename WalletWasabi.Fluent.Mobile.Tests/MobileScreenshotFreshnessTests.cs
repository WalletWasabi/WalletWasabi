using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileScreenshotFreshnessTests
{
	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void CaptureReflectsTextAndCollectionChangesFromTheCurrentInteraction(bool dark)
	{
		var fixture = new MobileWalletFixture();
		fixture.Navigate("history");
		var view = new MobileWalletSurface { DataContext = fixture };
		var window = new Window
		{
			Width = 390, Height = 844, SystemDecorations = SystemDecorations.None,
			Content = view, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
		};
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			var suffix = dark ? "dark" : "light";
			var before = MobileScreenshot.Capture(window, $"capture-freshness-before-{suffix}", "history-before-search");
			view.FindControl<TextBox>("TransactionQuery")!.Text = "no matching transaction";
			Dispatcher.UIThread.RunJobs();
			Assert.True(fixture.IsEmpty);
			var after = MobileScreenshot.Capture(window, $"capture-freshness-after-{suffix}", "empty-history-after-search");
			Assert.False(before.SequenceEqual(after), "Capture returned the preceding framebuffer after search changed the view.");
			Assert.Empty(view.FindControl<ListBox>("HistoryRows")!.Items);
			Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(),
				text => text.Text == "No matching transactions" && MobileScreenshot.IsShown(text));
		}
		finally { window.Close(); }
	}
}
