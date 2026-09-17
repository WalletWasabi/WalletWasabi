using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobilePreferencesTests
{
	[AvaloniaTheory]
	[InlineData(320, false)]
	[InlineData(320, true)]
	[InlineData(390, false)]
	[InlineData(390, true)]
	[InlineData(430, false)]
	[InlineData(430, true)]
	public void PreferencesPreserveTwoWayBindingsAndReadOnlyInterlocks(int width, bool dark)
	{
		var state = new PreferencesFixture { IsDark = dark };
		using var errors = new MobileScreenshot.BindingErrors();
		var panel = new MobilePreferencesPanel { IsReadOnly = false };
		panel.Bind(MobilePreferencesPanel.IsDarkProperty, new Binding(nameof(state.IsDark)) { Source = state, Mode = BindingMode.TwoWay });
		panel.Bind(MobilePreferencesPanel.PrivacyModeProperty, new Binding(nameof(state.PrivacyMode)) { Source = state, Mode = BindingMode.TwoWay });
		panel.Bind(MobilePreferencesPanel.AutoCopyProperty, new Binding(nameof(state.AutoCopy)) { Source = state, Mode = BindingMode.TwoWay });
		panel.Bind(MobilePreferencesPanel.AutoPasteProperty, new Binding(nameof(state.AutoPaste)) { Source = state, Mode = BindingMode.TwoWay });
		var window = new Window { Width = width, Height = 844, SystemDecorations = SystemDecorations.None,
			RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
			Content = new MobilePage { Header = "Settings", ShowBack = true,
				Content = new ScrollViewer { Content = new Border { Padding = new Thickness(16,8,16,16), Child = panel } } } };
		try
		{
			window.Show(); Pump();
			MobileScreenshot.Capture(window, $"preferences-{width}-{(dark ? "dark" : "light")}", "preferences", "bound-preference-controls");
			var appearance = panel.FindControl<MobileAppearanceSelector>("Appearance")!;
			var light = appearance.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PART_Light");
			var night = appearance.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "PART_Dark");
			Press(window, light); Assert.False(state.IsDark);
			Press(window, night); Assert.True(state.IsDark);
			Press(window, night); Assert.True(state.IsDark);
			state.IsDark = false; Pump(); Assert.False(appearance.IsDark);
			foreach (var name in new[] { "DiscreetMode", "AutoCopyToggle", "AutoPasteToggle" })
			{
				var toggle = panel.FindControl<MobilePreferenceToggle>(name)!;
				Assert.False(toggle.IsChecked ?? false);
				Press(window, toggle); Assert.True(toggle.IsChecked);
				Assert.Equal(toggle.Label, Avalonia.Automation.AutomationProperties.GetName(toggle));
				Assert.True(toggle.Bounds.Height >= 48);
			}
			Assert.True(state.PrivacyMode && state.AutoCopy && state.AutoPaste);
			panel.IsReadOnly = true; Pump();
			Assert.All(panel.GetVisualDescendants().OfType<Button>(), button => Assert.False(button.IsEffectivelyEnabled));
			MobileScreenshot.AssertNoHorizontalOverflow(window);
			Assert.Empty(errors.Errors);
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void MissingSettingsStayReadOnly()
	{
		Assert.True(new MobilePreferencesPanel().IsReadOnly);
	}

	private static void Press(Window window, Control control)
	{
		control.BringIntoView(); Pump(); Assert.True(control.Focus());
		window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
		window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None); Pump();
	}
	private static void Pump() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
	private sealed class PreferencesFixture : ReactiveObject
	{
		private bool _dark, _privacy, _copy, _paste;
		public bool IsDark { get => _dark; set => this.RaiseAndSetIfChanged(ref _dark, value); }
		public bool PrivacyMode { get => _privacy; set => this.RaiseAndSetIfChanged(ref _privacy, value); }
		public bool AutoCopy { get => _copy; set => this.RaiseAndSetIfChanged(ref _copy, value); }
		public bool AutoPaste { get => _paste; set => this.RaiseAndSetIfChanged(ref _paste, value); }
	}
}
