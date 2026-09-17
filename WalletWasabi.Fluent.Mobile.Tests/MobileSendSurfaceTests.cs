using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileSendSurfaceTests
{
	public static IEnumerable<object[]> Cases()
	{
		foreach (var width in new[] { 320, 390, 430 })
			foreach (var dark in new[] { false, true })
				foreach (var invalid in new[] { false, true })
					yield return new object[] { width, dark, invalid };
	}

	[AvaloniaTheory]
	[MemberData(nameof(Cases))]
	public void PopulatedNativeSendAndValidationStatesRender(int width, bool dark, bool invalid)
	{
		using var fixture = new MobileSendFixture();
		using var bindings = new MobileScreenshot.BindingErrors();
		var surface = new MobileSendSurface { DataContext = fixture };
		var window = CreateWindow(surface, width, dark);
		try
		{
			window.Show(); Pump();
			if (invalid) surface.FindControl<TextBox>("RecipientInput")!.Text = "invalid-address";
			Pump();
			MobileScreenshot.Capture(window, $"send-{(invalid ? "invalid" : "ready")}-{width}-{(dark ? "dark" : "light")}", "send-draft", "bound-send-presentation");
			Assert.Equal(fixture.To, surface.FindControl<TextBox>("RecipientInput")!.Text);
			Assert.Equal(invalid, fixture.HasErrors);
			Assert.Equal(invalid, DataValidationErrors.GetHasErrors(surface.FindControl<TextBox>("RecipientInput")!));
			Assert.Equal(fixture.AmountBtc, surface.FindControl<DualCurrencyEntryBox>("AmountInput")!.AmountBtc);
			MobileScreenshot.AssertNoHorizontalOverflow(window);
			Assert.Empty(bindings.Errors);
		}
		finally { window.Close(); }
	}

	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void NativeEditorForwardsInputActionsAndFeeChoices(bool dark)
	{
		using var fixture = new MobileSendFixture();
		using var bindings = new MobileScreenshot.BindingErrors();
		var surface = new MobileSendSurface { DataContext = fixture };
		var window = CreateWindow(surface, 390, dark);
		try
		{
			window.Show(); Pump();
			Press(window, surface.FindControl<Button>("PasteRecipient")!);
			Press(window, surface.FindControl<Button>("ScanRecipient")!);
			Press(window, surface.FindControl<Button>("MaximumAmount")!);
			Assert.Equal(new[] { "paste", "scan", "max" }, fixture.Invocations);
			Assert.Equal(0.2456m, surface.FindControl<DualCurrencyEntryBox>("AmountInput")!.AmountBtc);
			surface.FindControl<DualCurrencyEntryBox>("AmountInput")!.AmountBtc = 0.015m;
			Pump(); Assert.Equal(0.015m, fixture.AmountBtc);
			var priority = surface.GetVisualDescendants().OfType<Button>().Single(button => button.DataContext is MobileFeeTargetOption { RequestedBlocks: 1 });
			Press(window, priority);
			Assert.Equal(1, fixture.SelectedTarget);
			Assert.True(fixture.FeeSelection.Options.Single(option => option.RequestedBlocks == 1).IsSelected);
			fixture.LockPayment(); Pump();
			Assert.True(surface.FindControl<TextBox>("RecipientInput")!.IsReadOnly);
			Assert.True(surface.FindControl<DualCurrencyEntryBox>("AmountInput")!.IsReadOnly);
			Assert.False(surface.FindControl<Button>("MaximumAmount")!.IsEffectivelyEnabled);
			fixture.IsBusy = true; Pump();
			Assert.All(surface.GetVisualDescendants().OfType<Button>(), button => Assert.False(button.IsEffectivelyEnabled));
			Assert.Empty(bindings.Errors);
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void ReplacingTheCallerOwnedPresentationDoesNotDisposeEitherFeeSelection()
	{
		using var first = new MobileSendFixture();
		using var second = new MobileSendFixture();
		second.AmountBtc = 0.02m;
		var surface = new MobileSendSurface { DataContext = first };
		var window = CreateWindow(surface, 390, true);
		try
		{
			window.Show(); Pump();
			surface.DataContext = second; Pump();
			first.AmountBtc = 0.1m; Pump();
			Assert.Equal(0.02m, surface.FindControl<DualCurrencyEntryBox>("AmountInput")!.AmountBtc);
			Assert.All(first.FeeSelection.Options, option => Assert.True(option.IsAvailable));
		}
		finally { window.Close(); }
		Assert.All(second.FeeSelection.Options, option => Assert.True(option.IsAvailable));
	}

	private static Window CreateWindow(Control surface, int width, bool dark) => new()
	{
		Width = width, Height = 844, SystemDecorations = SystemDecorations.None,
		RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
		Content = new MobilePage
		{
			Header = "Send Bitcoin", ShowBack = true,
			Footer = new Button { Content = "Review payment", Classes = { "mobile-button", "primary" }, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch },
			Content = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
				Content = new Border { Padding = new Thickness(16, 8, 16, 16), Child = surface } }
		}
	};
	private static void Press(Window window, Control control)
	{
		control.BringIntoView(); Pump(); Assert.True(control.Focus());
		window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
		window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
		Pump();
	}
	private static void Pump()
	{
		for (var i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
	}
}
