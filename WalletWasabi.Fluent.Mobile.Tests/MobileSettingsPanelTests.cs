using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.TextInput;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

/// <summary>Unbound layout and control-contract tests, not persistence or RPC connection tests.</summary>
public sealed class MobileSettingsPanelTests
{
	public static IEnumerable<object[]> Cases()
	{
		foreach (var name in new[] { "general", "bitcoin", "coordinator", "connections" })
			foreach (var width in new[] { 320, 390 })
				foreach (var dark in new[] { false, true })
					yield return new object[] { name, width, dark };
	}

	[AvaloniaTheory]
	[MemberData(nameof(Cases))]
	public void NativeSettingsPanelsRenderInsideNarrowViewports(string name, int width, bool dark)
	{
		Control panel = name switch
		{
			"general" => new MobileGeneralSettingsPanel(),
			"bitcoin" => new MobileBitcoinSettingsPanel(),
			"coordinator" => new MobileCoordinatorSettingsPanel(),
			"connections" => new MobileConnectionsSettingsPanel(),
			_ => throw new ArgumentOutOfRangeException(nameof(name))
		};
		var page = new MobilePage { Header = "Settings", Content = new ScrollViewer { Content = new Border { Padding = new Thickness(16), Child = panel } } };
		var window = new Window
		{
			Width = width, Height = 844, SystemDecorations = SystemDecorations.None,
			RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light, Content = page
		};
		try
		{
			window.Show(); Dispatcher.UIThread.RunJobs();
			MobileScreenshot.AssertNoHorizontalOverflow(page);
			MobileScreenshot.Capture(window, $"settings-unbound-{name}-{width}-{(dark ? "dark" : "light")}", name, "unbound-structure");
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void RpcCredentialControlDoesNotExposePlainTextOrUndoHistory()
	{
		var view = new MobileBitcoinSettingsPanel();
		var input = view.FindControl<TextBox>("RpcCredentials")!;
		Assert.Equal('●', input.PasswordChar);
		Assert.False(input.RevealPassword);
		Assert.False(input.IsUndoEnabled);
		Assert.True(TextInputOptions.GetIsSensitive(input));
		Assert.Equal(false, TextInputOptions.GetShowSuggestions(input));
	}

	[AvaloniaFact]
	public void SettingsFieldPreservesContentDataContext()
	{
		var model = new object();
		var input = new TextBox();
		var field = new MobileSettingsField { Header = "Field", Description = "Field description", Content = input, DataContext = model };
		field.Styles.Add(new StyleInclude(new Uri("avares://WalletWasabi.Fluent/"))
		{
			Source = new Uri("avares://WalletWasabi.Fluent/Mobile/Styles/MobileSettingsControls.axaml")
		});
		var window = new Window { Width = 320, Height = 568, Content = field };
		try
		{
			window.Show(); Dispatcher.UIThread.RunJobs();
			Assert.Same(model, input.DataContext);
			var replacement = new TextBox(); field.Content = replacement;
			Assert.Same(model, replacement.DataContext);
			Assert.Null(input.DataContext);
		}
		finally { window.Close(); }
	}
}
