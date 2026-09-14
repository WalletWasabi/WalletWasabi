using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileWalletVisualTests
{
	public static IEnumerable<object[]> Cases()
	{
		foreach (var section in new[] { "home", "history", "privacy", "coinjoin", "coins", "transaction", "discover" })
			foreach (var size in new[] { (320, 568), (390, 844), (430, 932) })
				foreach (var dark in new[] { false, true })
					yield return new object[] { section, size.Item1, size.Item2, dark };
	}

	[AvaloniaTheory]
	[MemberData(nameof(Cases))]
	public void PopulatedProductionSurfacesRenderAllDestinations(string section, int width, int height, bool dark)
	{
		using var culture = new FixedCulture();
		using var bindings = new MobileScreenshot.BindingErrors();
		var fixture = new MobileWalletFixture();
		fixture.Navigate(section);
		var view = new MobileWalletSurface { DataContext = fixture };
		var window = CreateWindow(view, width, height, dark);
		try
		{
			window.Show(); Pump();
			MobileScreenshot.Capture(window, $"wallet-{section}-{width}-{height}-{(dark ? "dark" : "light")}", section);
			Assert.Same(fixture, view.FindControl<Grid>("PresentationRoot")!.DataContext);
			Assert.InRange(view.Bounds.Width, width - 1, width + 1);
			var pages = new[] { "HomePage", "HistoryPage", "PrivacyPage", "CoinJoinPage", "CoinsPage", "TransactionPage", "DiscoverPage" };
			Assert.Single(pages.Select(name => view.FindControl<Control>(name)!).Where(control => control.IsVisible));
			MobileScreenshot.AssertNoHorizontalOverflow(view);
			Assert.Empty(bindings.Errors);
		}
		finally { window.Close(); }
	}

	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void NativeBindingsDriveNavigationFilteringDiscreetModeAndCoinSelection(bool dark)
	{
		using var culture = new FixedCulture();
		using var bindings = new MobileScreenshot.BindingErrors();
		var fixture = new MobileWalletFixture();
		var view = new MobileWalletSurface { DataContext = fixture };
		var window = CreateWindow(view, 390, 844, dark);
		try
		{
			window.Show(); Pump();
			Press(window, view.FindControl<MobileActionButton>("SendAction")!);
			Press(window, view.FindControl<MobileActionButton>("ReceiveAction")!);
			Assert.Equal(new[] { "send", "receive" }, fixture.Invocations);
			Press(window, view.FindControl<Button>("DiscreetAction")!);
			Assert.Equal("•••••• BTC", view.FindControl<TextBlock>("WalletBalance")!.Text);
			MobileScreenshot.Capture(window, $"wallet-discreet-{(dark ? "dark" : "light")}", "discreet");
			Press(window, view.FindControl<Button>("AllTransactionsAction")!);
			Assert.Equal("history", fixture.Section);
			Press(window, view.FindControl<Button>("SentFilter")!);
			Assert.Equal("Sent", fixture.Filter);
			Assert.Single(fixture.Transactions);
			Assert.True(view.FindControl<Button>("SentFilter")!.Classes.Contains("selected"));
			view.FindControl<TextBox>("TransactionQuery")!.Text = "no matching transaction";
			Pump(); Assert.True(fixture.IsEmpty);
			MobileScreenshot.Capture(window, $"wallet-empty-search-{(dark ? "dark" : "light")}", "empty-search");
			view.FindControl<TextBox>("TransactionQuery")!.Text = "";
			Press(window, view.FindControl<Button>("AllFilter")!);
			fixture.Navigate("coins"); Pump();
			var row = view.FindControl<ListBox>("CoinRows")!.GetVisualDescendants().OfType<CheckBox>().First();
			Assert.False(view.FindControl<Button>("ExcludeCoinsAction")!.IsEffectivelyEnabled);
			Press(window, row);
			Assert.True(fixture.Coins.First().IsSelected);
			Press(window, view.FindControl<Button>("ExcludeCoinsAction")!);
			Assert.Contains("excluded", fixture.Message);
			Assert.Equal("Excluded from CoinJoin", fixture.Coins.First().Status);
			MobileScreenshot.Capture(window, $"wallet-selected-coins-{(dark ? "dark" : "light")}", "selected-coins");
			Assert.Empty(bindings.Errors);
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void TransactionRowUsesItsRealCommandAndDetailsReturnToHistory()
	{
		var fixture = new MobileWalletFixture();
		fixture.Navigate("history");
		var view = new MobileWalletSurface { DataContext = fixture };
		var window = CreateWindow(view, 390, 844, true);
		try
		{
			window.Show(); Pump();
			var row = fixture.Transactions.First();
			var action = view.FindControl<ListBox>("HistoryRows")!.GetVisualDescendants().OfType<Button>()
				.Single(button => ReferenceEquals(button.Command, row.OpenCommand));
			Press(window, action);
			Assert.Equal("transaction", fixture.Section);
			Assert.Same(row, fixture.SelectedTransaction);
			Press(window, view.FindControl<Button>("BackToHistoryAction")!);
			Assert.Equal("history", fixture.Section);
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void ThemeRoundTripRestoresTheSameRenderedPixels()
	{
		using var culture = new FixedCulture();
		var fixture = new MobileWalletFixture(); fixture.Navigate("privacy");
		var view = new MobileWalletSurface { DataContext = fixture };
		var window = CreateWindow(view, 390, 844, false);
		try
		{
			window.Show(); Pump();
			var light = MobileScreenshot.Capture(window, "roundtrip-light-before", "privacy");
			window.RequestedThemeVariant = ThemeVariant.Dark; Pump();
			var dark = MobileScreenshot.Capture(window, "roundtrip-dark", "privacy");
			Assert.False(light.SequenceEqual(dark));
			window.RequestedThemeVariant = ThemeVariant.Light; Pump();
			var restored = MobileScreenshot.Capture(window, "roundtrip-light-after", "privacy");
			Assert.True(light.SequenceEqual(restored), "A light/dark/light cycle changed the native view's rendered state.");
		}
		finally { window.Close(); }
	}

	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void CriticalPhaseKeepsTheBoundStopCommandDisabled(bool dark)
	{
		var fixture = new MobileWalletFixture(); fixture.Navigate("coinjoin"); fixture.SetCriticalPhase(true);
		var view = new MobileWalletSurface { DataContext = fixture };
		var window = CreateWindow(view, 390, 844, dark);
		try
		{
			window.Show(); Pump();
			var stop = view.GetVisualDescendants().OfType<Button>().First(button =>
				MobileScreenshot.IsShown(button) && ReferenceEquals(button.Command, fixture.CoinJoin.StopPauseCommand));
			Assert.False(stop.IsEffectivelyEnabled);
			MobileScreenshot.Capture(window, $"wallet-critical-phase-{(dark ? "dark" : "light")}", "critical-phase");
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void HistoryRemainsVirtualizedWithTwoThousandTransactions()
	{
		var fixture = new MobileWalletFixture(2000); fixture.Navigate("history");
		var view = new MobileWalletSurface { DataContext = fixture };
		var window = CreateWindow(view, 390, 844, true);
		try
		{
			window.Show(); Pump();
			var list = view.FindControl<ListBox>("HistoryRows")!;
			Assert.InRange(list.GetVisualDescendants().OfType<ListBoxItem>().Count(), 1, 100);
			MobileScreenshot.AssertNoHorizontalOverflow(view);
			var last = fixture.Transactions.Last(); list.ScrollIntoView(last); Pump();
			Assert.InRange(list.GetVisualDescendants().OfType<ListBoxItem>().Count(), 1, 100);
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void ReplacingOrDetachingFixtureDoesNotLeaveItsDataOnTheSurface()
	{
		var first = new MobileWalletFixture(); var second = new MobileWalletFixture(); second.Navigate("privacy");
		var view = new MobileWalletSurface { DataContext = first };
		var window = CreateWindow(view, 390, 844, true);
		try
		{
			window.Show(); Pump(); view.DataContext = second; Pump();
			Assert.Same(second, view.FindControl<Grid>("PresentationRoot")!.DataContext);
			first.Navigate("discover"); Pump(); Assert.True(view.FindControl<ScrollViewer>("PrivacyPage")!.IsVisible);
			window.Content = null; Pump(); Assert.Null(view.FindControl<Grid>("PresentationRoot")!.DataContext);
		}
		finally { window.Close(); }
	}

	private static Window CreateWindow(Control content, int width, int height, bool dark) => new()
	{
		Width = width, Height = height, SystemDecorations = SystemDecorations.None,
		RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light, Content = content
	};
	private static void Pump() => Dispatcher.UIThread.RunJobs();
	private static void Press(Window window, InputElement control)
	{
		Assert.True(control.Focus());
		window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
		window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None); Pump();
	}
	private sealed class FixedCulture : IDisposable
	{
		private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
		private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
		public FixedCulture() { CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture; }
		public void Dispose() { CultureInfo.CurrentCulture = _culture; CultureInfo.CurrentUICulture = _uiCulture; }
	}
}
