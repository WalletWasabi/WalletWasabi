using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileControlStateTests
{
	[Theory]
	[InlineData("home", "home", true)]
	[InlineData("history", "history", true)]
	[InlineData("transaction", "history", true)]
	[InlineData("transaction", "privacy", false)]
	[InlineData("privacy", "privacy", true)]
	[InlineData("coins", "privacy", true)]
	[InlineData("coinjoin", "privacy", true)]
	[InlineData("coins", "history", false)]
	[InlineData("discover", "discover", true)]
	[InlineData("unknown", "home", false)]
	[InlineData(null, "home", false)]
	[InlineData("home", null, false)]
	public void NestedPagesKeepTheCorrectNavigationTab(string? page, string? tab, bool expected)
	{
		var converter = new MobileNavigationSectionConverter();
		Assert.Equal(expected, Assert.IsType<bool>(converter.Convert(page, typeof(bool), tab, CultureInfo.InvariantCulture)));
	}

	[AvaloniaFact]
	public void BusyPageDisablesHeaderBodyAndFooterControls()
	{
		var body = new TextBox { Text = "Editable field" };
		var action = new Button { Content = "Close" };
		var footer = new Button { Content = "Continue" };
		// Deliberately no route context: tests the control template independently of wallet services.
		var page = new MobilePage { Header = "Review", ShowBack = true, Content = body, HeaderActions = action, Footer = footer };
		var window = CreateWindow(page, 390);
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			Assert.True(body.IsEffectivelyEnabled);
			Assert.True(action.IsEffectivelyEnabled);
			Assert.True(footer.IsEffectivelyEnabled);
			page.IsBusy = true;
			Dispatcher.UIThread.RunJobs();
			Assert.False(body.IsEffectivelyEnabled);
			Assert.False(action.IsEffectivelyEnabled);
			Assert.False(footer.IsEffectivelyEnabled);
			Assert.All(page.GetVisualDescendants().OfType<Button>(), button => Assert.False(button.IsEffectivelyEnabled));
			page.IsBusy = false;
			Dispatcher.UIThread.RunJobs();
			Assert.True(body.IsEffectivelyEnabled);
			Assert.True(action.IsEffectivelyEnabled);
			Assert.True(footer.IsEffectivelyEnabled);
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void SensitiveContentStaysHiddenUntilExplicitlyRevealed()
	{
		var content = new MobileSensitiveContent { Content = new TextBlock { Text = "Synthetic private value" } };
		content.Styles.Add(new StyleInclude(new Uri("avares://WalletWasabi.Fluent/"))
		{
			Source = new Uri("avares://WalletWasabi.Fluent/Mobile/Styles/MobileFlowControls.axaml")
		});
		var window = CreateWindow(content, 390);
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			var presenter = content.GetVisualDescendants().OfType<ContentPresenter>().Single(x => x.Name == "PART_ContentPresenter");
			var replacement = content.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "PART_Redacted");
			Assert.True(content.IsHidden);
			Assert.False(presenter.IsVisible);
			Assert.True(replacement.IsVisible);
			content.IsHidden = false;
			Dispatcher.UIThread.RunJobs();
			Assert.True(presenter.IsVisible);
			Assert.False(replacement.IsVisible);
			content.IsHidden = true;
			Dispatcher.UIThread.RunJobs();
			Assert.False(presenter.IsVisible);
			Assert.True(replacement.IsVisible);
		}
		finally { window.Close(); }
	}

	[AvaloniaTheory]
	[InlineData(320)]
	[InlineData(390)]
	public void ThemeSwitchUpdatesNavigationWithoutRecreatingTheControl(int width)
	{
		var navigation = new MobileWalletNavigation();
		var window = CreateWindow(navigation, width);
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			var light = navigation.FindControl<Control>("LightNavigation")!;
			var dark = navigation.FindControl<Control>("DarkNavigation")!;
			Assert.True(light.IsVisible);
			Assert.False(dark.IsVisible);
			Assert.Equal(5, light.GetVisualDescendants().OfType<MobileActionButton>().Count());
			Assert.All(light.GetVisualDescendants().OfType<MobileActionButton>(), button => Assert.True(button.Bounds.Width >= 48));
			window.RequestedThemeVariant = ThemeVariant.Dark;
			Dispatcher.UIThread.RunJobs();
			Assert.False(light.IsVisible);
			Assert.True(dark.IsVisible);
			Assert.Equal(4, dark.GetVisualDescendants().OfType<MobileActionButton>().Count());
			Assert.All(dark.GetVisualDescendants().OfType<MobileActionButton>(), button => Assert.True(button.Bounds.Width >= 48));
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void PrivacyThemeChangesDoNotAlterCoinJoinProgressGeometry()
	{
		var view = new MobileWalletSurface();
		var window = CreateWindow(view, 390);
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			var privacy = view.FindControl<MobilePrivacyRing>("PrivacyBalanceGauge")!;
			var coinjoin = view.FindControl<MobilePrivacyRing>("CoinJoinProgressRing")!;
			Assert.Equal(135d, privacy.StartAngle);
			Assert.Equal(270d, privacy.SweepAngle);
			Assert.Equal(-90d, coinjoin.StartAngle);
			Assert.Equal(360d, coinjoin.SweepAngle);
			window.RequestedThemeVariant = ThemeVariant.Dark;
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(-90d, privacy.StartAngle);
			Assert.Equal(360d, privacy.SweepAngle);
			Assert.Equal(-90d, coinjoin.StartAngle);
			Assert.Equal(360d, coinjoin.SweepAngle);
		}
		finally { window.Close(); }
	}

	private static Window CreateWindow(Control content, int width) => new()
	{
		Width = width, Height = 844, SystemDecorations = SystemDecorations.None,
		Content = content, RequestedThemeVariant = ThemeVariant.Light
	};
}
