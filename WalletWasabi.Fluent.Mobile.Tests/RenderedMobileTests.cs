using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Windows.Input;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class RenderedMobileTests
{
	public static IEnumerable<object[]> EntryCases()
	{
		foreach (var name in new[] { "welcome", "add-wallet", "wallet-name", "login", "loading", "wallet-list" })
			foreach (var width in new[] { 320, 390 })
				foreach (var dark in new[] { false, true })
					yield return new object[] { name, width, dark };
	}

	[AvaloniaTheory]
	[MemberData(nameof(EntryCases))]
	public void EntryViewsRenderWithoutWalletServices(string name, int width, bool dark)
	{
		Control view = name switch
		{
			"welcome" => new MobileWelcomeView(),
			"add-wallet" => new MobileAddWalletView(),
			"wallet-name" => new MobileWalletNameView(),
			"login" => new MobileLoginView(),
			"loading" => new MobileLoadingView(),
			"wallet-list" => new MobileWalletsListView(),
			_ => throw new ArgumentOutOfRangeException(nameof(name))
		};
		var window = CreateWindow(view, width, dark);
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			foreach (var scroll in view.GetVisualDescendants().OfType<ScrollViewer>())
			{
				if (!scroll.IsVisible || scroll.Viewport.Width <= 0) continue;
				Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1,
					$"{name}: horizontal content exceeds the {width}-DIP viewport.");
			}
			SaveFrame(window, $"entry-unbound-{name}-{width}-{(dark ? "dark" : "light")}");
		}
		finally { window.Close(); }
	}

	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void PopulatedNativeComponentSceneRenders(bool dark)
	{
		var panel = new StackPanel { Spacing = 16, Margin = new Thickness(16) };
		panel.Children.Add(new TextBlock { Text = "Native component reference", Classes = { "mobile-title" } });
		panel.Children.Add(new TextBlock { Text = "Synthetic test content; no wallet or funds are loaded.", Classes = { "mobile-caption" } });
		var ring = new MobilePrivacyRing { Width = 140, Height = 140, Value = 78 };
		ring.Bind(MobilePrivacyRing.ForegroundProperty, new DynamicResourceExtension("MobileAccent"));
		ring.Bind(MobilePrivacyRing.TrackProperty, new DynamicResourceExtension("MobileRingTrack"));
		panel.Children.Add(new Border { Child = ring, Classes = { "mobile-card" } });
		panel.Children.Add(new MobileChoiceButton { Label = "Receive Bitcoin", Description = "Create a fresh address for this sender.", Icon = "receive" });
		panel.Children.Add(new TextBox { Text = "0.01000000", Classes = { "mobile-input" } });
		panel.Children.Add(new MobileActionButton { Label = "Privacy", Icon = "shield", IsSelected = true });
		var page = new MobilePage
		{
			Header = "Wasabi Wallet", Content = new ScrollViewer { Content = panel },
			Footer = new Button { Content = "Review payment", Classes = { "mobile-button", "primary" }, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch }
		};
		var window = CreateWindow(page, 390, dark);
		try { window.Show(); SaveFrame(window, $"components-{(dark ? "dark" : "light")}"); }
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void FooterCommandWorksAfterTemplatingAndDataContextReplacement()
	{
		var first = new CommandHost();
		var second = new CommandHost();
		var button = new Button { Content = "Continue", Classes = { "mobile-button", "primary" } };
		button.Bind(Button.CommandProperty, new Binding(nameof(CommandHost.Command)));
		var page = new MobilePage { DataContext = first, Header = "Review", Footer = button, Content = new TextBlock { Text = "Body" } };
		var window = CreateWindow(page, 390, false);
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			Assert.Same(first, button.DataContext);
			Assert.True(button.Focus());
			window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
			window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
			Assert.Equal(1, first.Calls);
			page.DataContext = second;
			Dispatcher.UIThread.RunJobs();
			window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
			window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
			Assert.Equal(1, first.Calls);
			Assert.Equal(1, second.Calls);
			Assert.True(button.Bounds.Height >= 48);
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void ChoiceButtonWrapsLongLabelsAndExecutesByKeyboard()
	{
		var host = new CommandHost();
		var button = new MobileChoiceButton
		{
			Label = "A deliberately long wallet name that must wrap without clipping",
			Description = "This text validates the narrow-phone navigation row layout.", Command = host.Command
		};
		var window = CreateWindow(new Border { Padding = new Thickness(16), Child = button }, 320, true);
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			Assert.InRange(button.Bounds.Width, 48, 288);
			Assert.Equal(button.Label, Avalonia.Automation.AutomationProperties.GetName(button));
			Assert.True(button.Focus());
			window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
			window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
			Assert.Equal(1, host.Calls);
		}
		finally { window.Close(); }
	}

	private static Window CreateWindow(Control content, int width, bool dark) => new()
	{
		Width = width, Height = 844, SystemDecorations = SystemDecorations.None,
		Content = content, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
	};

	private static void SaveFrame(Window window, string name)
	{
		using var frame = window.CaptureRenderedFrame();
		Assert.NotNull(frame);
		Assert.True(frame.PixelSize.Width > 0 && frame.PixelSize.Height > 0);
		var directory = Environment.GetEnvironmentVariable("WASABI_MOBILE_TEST_ARTIFACTS") ?? Path.Combine(AppContext.BaseDirectory, "mobile-previews");
		Directory.CreateDirectory(directory);
		frame.Save(Path.Combine(directory, name + ".png"));
	}

	public sealed class CommandHost
	{
		public CommandHost() => Command = new CountingCommand(() => Calls++);
		public ICommand Command { get; }
		public int Calls { get; private set; }
	}

	private sealed class CountingCommand(Action execute) : ICommand
	{
		public event EventHandler? CanExecuteChanged { add { } remove { } }
		public bool CanExecute(object? parameter) => true;
		public void Execute(object? parameter) => execute();
	}
}
