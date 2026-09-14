using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileBackupViewTests
{
	public static IEnumerable<object[]> Cases()
	{
		foreach (var name in new[] { "backup-type", "recovery-words", "confirm-words", "share-options", "share-words",
			"confirm-share", "recover", "recover-shares", "passphrase", "advanced-recovery", "wallet-added", "error" })
			foreach (var width in new[] { 320, 390 })
				foreach (var dark in new[] { false, true })
					yield return new object[] { name, width, dark };
	}

	/// <summary>Unbound construction/layout smoke tests. Never generate or render a real recovery phrase.</summary>
	[AvaloniaTheory]
	[MemberData(nameof(Cases))]
	public void NativeBackupFlowsLoadAtNarrowPhoneWidths(string name, int width, bool dark)
	{
		Control view = name switch
		{
			"backup-type" => new MobileWalletBackupTypeView(),
			"recovery-words" => new MobileRecoveryWordsView(),
			"confirm-words" => new MobileConfirmRecoveryWordsView(),
			"share-options" => new MobileMultiShareOptionsView(),
			"share-words" => new MobileMultiShareView(),
			"confirm-share" => new MobileConfirmMultiShareView(),
			"recover" => new MobileRecoverWalletView(),
			"recover-shares" => new MobileRecoverMultiShareWalletView(),
			"passphrase" => new MobileCreatePasswordView(),
			"advanced-recovery" => new MobileAdvancedRecoveryOptionsView(),
			"wallet-added" => new MobileAddedWalletView(),
			"error" => new MobileErrorView(),
			_ => throw new ArgumentOutOfRangeException(nameof(name))
		};
		var window = new Window
		{
			Width = width, Height = 844, SystemDecorations = SystemDecorations.None,
			RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light, Content = view
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
					$"{name} exceeds the {width}-DIP viewport.");
			}
			Assert.All(view.GetVisualDescendants().OfType<MobileSecretPanel>(), panel => Assert.False(panel.IsRevealed));
			using var frame = window.CaptureRenderedFrame();
			Assert.NotNull(frame);
			var directory = Environment.GetEnvironmentVariable("WASABI_MOBILE_TEST_ARTIFACTS")
				?? Path.Combine(AppContext.BaseDirectory, "mobile-previews");
			Directory.CreateDirectory(directory);
			frame.Save(Path.Combine(directory, $"backup-unbound-{name}-{width}-{(dark ? "dark" : "light")}.png"));
		}
		finally { window.Close(); }
	}

	[Fact]
	public void BackupLocatorLeavesUnknownModelsToTheExistingLocators()
	{
		var locator = new MobileBackupViewLocator();
		Assert.False(locator.Match(null));
		Assert.False(locator.Match(new object()));
		Assert.Throws<ArgumentException>(() => locator.Build(new object()));
	}
}
