using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using NBitcoin;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.Mobile.Views;
using Gma.QrCodeNet.Encoding;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileReceiveSurfaceTests
{
	// Deterministic regtest-only fixture. No user wallet, mainnet balance or network is used.
	private static string Address
	{
		get
		{
			using var key = new Key(Enumerable.Repeat((byte)0x42, 32).ToArray());
			return key.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.RegTest).ToString();
		}
	}

	public static IEnumerable<object[]> Cases()
	{
		foreach (var scenario in new[] { "address", "amount", "invalid", "qr-error" })
			foreach (var size in new[] { (320, 568), (390, 844), (430, 932) })
				foreach (var dark in new[] { false, true })
					yield return new object[] { scenario, size.Item1, size.Item2, dark };
	}

	[AvaloniaTheory]
	[MemberData(nameof(Cases))]
	public void ProductionReceiveSurfaceRendersValidatedQrAndFailureStates(string scenario, int width, int height, bool dark)
	{
		using var culture = new CultureScope();
		using var bindings = new MobileScreenshot.BindingErrors();
		using var state = CreateState(failQr: scenario == "qr-error");
		if (scenario is "amount" or "invalid") state.Amount = scenario == "amount" ? "0.015" : "0.000000001";
		var view = new MobileReceiveSurface { DataContext = state };
		var window = CreateWindow(view, width, height, dark);
		try
		{
			window.Show(); Pump();
			MobileScreenshot.Capture(window, $"receive-{scenario}-{width}-{height}-{(dark ? "dark" : "light")}",
				"receive-" + scenario, "bound-regtest-presentation");
			Assert.Equal(Address, view.FindControl<SelectableTextBlock>("ReceivingAddress")!.Text);
			Assert.Same(state.Matrix, view.FindControl<MobileQrCode>("RequestQr")!.Matrix);
			Assert.Equal(scenario != "invalid", view.FindControl<Button>("CopyReceive")!.IsEffectivelyEnabled);
			Assert.Equal(scenario is "amount" or "invalid", view.FindControl<StackPanel>("AmountEditor")!.IsVisible);
			if (scenario is "address" or "amount") AssertEncodedPayload(state);
			else Assert.Null(state.Matrix);
			MobileScreenshot.AssertNoHorizontalOverflow(window);
			Assert.Empty(bindings.Errors);
		}
		finally { window.Close(); }
	}

	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void KeyboardModesAndAmountBindingsCopyOnlyTheCurrentPayload(bool dark)
	{
		var copied = new List<string>();
		using var state = CreateState(copy: text => { copied.Add(text); return Task.CompletedTask; });
		using var bindings = new MobileScreenshot.BindingErrors();
		var view = new MobileReceiveSurface { DataContext = state };
		var window = CreateWindow(view, 390, 844, dark);
		try
		{
			window.Show(); Pump();
			Press(window, view.FindControl<Button>("RequestMode")!);
			Assert.True(state.IsRequestMode);
			Assert.Contains("selected", view.FindControl<Button>("RequestMode")!.Classes);
			view.FindControl<TextBox>("RequestedAmount")!.Text = "0.01500000";
			Pump(); AssertEncodedPayload(state);
			Press(window, view.FindControl<Button>("CopyReceive")!);
			Assert.Equal($"bitcoin:{Address}?amount=0.015", Assert.Single(copied));
			view.FindControl<TextBox>("RequestedAmount")!.Text = "not-an-amount";
			Pump(); Assert.Null(state.Matrix);
			Assert.False(view.FindControl<Button>("CopyReceive")!.IsEffectivelyEnabled);
			Press(window, view.FindControl<Button>("AddressMode")!);
			Assert.Empty(state.Amount);
			Assert.False(view.FindControl<StackPanel>("AmountEditor")!.IsVisible);
			Assert.Equal(Address, state.PaymentRequest);
			AssertEncodedPayload(state);
			Press(window, view.FindControl<Button>("CopyReceive")!);
			Assert.Equal(new[] { $"bitcoin:{Address}?amount=0.015", Address }, copied);
			Assert.Empty(bindings.Errors);
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void QrFailureCanRetryWithoutChangingTheRequestOrRequiringNetworkAccess()
	{
		var failures = true;
		using var state = new MobileReceivePresentation(Address,
			text => failures ? Observable.Throw<bool[,]>(new InvalidOperationException("fixture failure")) : Encode(text),
			_ => Task.CompletedTask, ImmediateScheduler.Instance);
		var view = new MobileReceiveSurface { DataContext = state };
		var window = CreateWindow(view, 390, 844, true);
		try
		{
			window.Show(); Pump();
			Assert.True(state.CanRetryQr);
			Assert.True(view.FindControl<Button>("CopyReceive")!.IsEffectivelyEnabled);
			failures = false;
			Press(window, view.FindControl<Button>("RetryQr")!);
			Assert.Empty(state.Error);
			Assert.False(state.CanRetryQr);
			Assert.Equal(Address, state.PaymentRequest);
			AssertEncodedPayload(state);
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void ClipboardFailureIsVisibleWithoutDiscardingTheValidQr()
	{
		using var state = CreateState(copy: _ => throw new InvalidOperationException("clipboard unavailable"));
		var view = new MobileReceiveSurface { DataContext = state };
		var window = CreateWindow(view, 390, 844, true);
		try
		{
			window.Show(); Pump();
			Press(window, view.FindControl<Button>("CopyReceive")!);
			Assert.Contains("clipboard", state.Error);
			Assert.True(view.FindControl<Border>("ReceiveError")!.IsVisible);
			Assert.True(view.FindControl<Button>("CopyReceive")!.IsEffectivelyEnabled);
			AssertEncodedPayload(state);
		}
		finally { window.Close(); }
	}

	private static MobileReceivePresentation CreateState(bool failQr = false, Func<string, Task>? copy = null) =>
		new(Address, text => failQr ? Observable.Throw<bool[,]>(new InvalidOperationException("fixture QR failure")) : Encode(text),
			copy ?? (_ => Task.CompletedTask), ImmediateScheduler.Instance);

	// Use the application's actual QR encoder, not a synthetic module pattern.
	private static IObservable<bool[,]> Encode(string text) => Observable.Return(new QrEncoder().Encode(text).Matrix.InternalArray);

	private static void AssertEncodedPayload(MobileReceivePresentation state)
	{
		var expected = new QrEncoder().Encode(state.PaymentRequest).Matrix.InternalArray;
		Assert.NotNull(state.Matrix);
		Assert.Equal(expected.GetLength(0), state.Matrix.GetLength(0));
		Assert.Equal(expected.GetLength(1), state.Matrix.GetLength(1));
		for (var x = 0; x < expected.GetLength(0); x++)
			for (var y = 0; y < expected.GetLength(1); y++)
				Assert.Equal(expected[x, y], state.Matrix[x, y]);
	}

	private static Window CreateWindow(Control view, int width, int height, bool dark) => new()
	{
		Width = width, Height = height, CanResize = false,
		RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
		Content = new MobilePage
		{
			Header = "Receive Bitcoin", ShowBack = true,
			Footer = new Button { Content = "Done", Classes = { "mobile-button", "primary" }, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch },
			Content = new ScrollViewer
			{
				HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
				Content = new Border { Padding = new Thickness(16, 8, 16, 16), Child = view }
			}
		}
	};

	private static void Press(Window window, Control control)
	{
		control.BringIntoView(); Pump();
		Assert.True(control.Focus());
		window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
		window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
		Pump();
	}

	private static void Pump()
	{
		for (var pass = 0; pass < 3; pass++)
		{
			Dispatcher.UIThread.RunJobs();
			AvaloniaHeadlessPlatform.ForceRenderTimerTick();
		}
	}

	private sealed class CultureScope : IDisposable
	{
		private readonly CultureInfo _previous = CultureInfo.CurrentCulture;
		public CultureScope() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		public void Dispose() => CultureInfo.CurrentCulture = _previous;
	}
}
