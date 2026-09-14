using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input.TextInput;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileAuthorizationViewTests
{
	public static IEnumerable<object[]> Cases()
	{
		foreach (var password in new[] { false, true })
			foreach (var width in new[] { 320, 390 })
				foreach (var dark in new[] { false, true })
					yield return [password, width, dark];
	}

	[AvaloniaTheory]
	[MemberData(nameof(Cases))]
	public void AuthorizationViewsRenderWithoutWalletOrSigningServices(bool password, int width, bool dark)
	{
		Control view = password ? new MobilePasswordAuthorizationView() : new MobileHardwareAuthorizationView();
		var window = new Window
		{
			Width = width, Height = 844, SystemDecorations = SystemDecorations.None,
			RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
			Content = view
		};
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			Assert.InRange(view.Bounds.Width, 1, width);
			foreach (var scroll in view.GetVisualDescendants().OfType<ScrollViewer>())
			{
				if (scroll.Viewport.Width > 0)
					Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
			}
			if (view is MobilePasswordAuthorizationView passphraseView)
			{
				var input = passphraseView.FindControl<TextBox>("PassphraseInput")!;
				Assert.Equal('●', input.PasswordChar);
				Assert.False(input.RevealPassword);
				Assert.False(input.IsUndoEnabled);
				Assert.True(TextInputOptions.GetIsSensitive(input));
				Assert.Equal(false, TextInputOptions.GetShowSuggestions(input));
				Assert.False(TextInputOptions.GetAutoCapitalization(input));
				Assert.True(input.Bounds.Height >= 48);
			}
			MobileScreenshot.Capture(window, $"authorization-unbound-{(password ? "password" : "hardware")}-{width}-{(dark ? "dark" : "light")}",
				password ? "password-authorization" : "hardware-authorization", "unbound-layout");
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void LoginPassphraseHasTheSameSensitiveInputPolicy()
	{
		var view = new MobileLoginView();
		var input = view.FindControl<TextBox>("PassphraseInput")!;
		Assert.Equal('●', input.PasswordChar);
		Assert.False(input.RevealPassword);
		Assert.False(input.IsUndoEnabled);
		Assert.True(TextInputOptions.GetIsSensitive(input));
		Assert.Equal(false, TextInputOptions.GetShowSuggestions(input));
		Assert.False(TextInputOptions.GetAutoCapitalization(input));
	}

	[Fact]
	public void FactoryDoesNotReplaceUnrelatedRoutes()
	{
		var locator = new MobileAuthorizationViewLocator();
		Assert.False(locator.Match(null));
		Assert.False(locator.Match(new object()));
		Assert.Throws<ArgumentException>(() => locator.Build(null));
		Assert.Throws<ArgumentException>(() => locator.Build(new object()));
	}
}
