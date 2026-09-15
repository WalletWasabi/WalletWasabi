using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Gma.QrCodeNet.Encoding;
using NBitcoin;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileShareSurfaceTests
{
	[AvaloniaTheory]
	[InlineData(320, false)]
	[InlineData(320, true)]
	[InlineData(390, false)]
	[InlineData(390, true)]
	[InlineData(430, false)]
	[InlineData(430, true)]
	public void NativeReceiveShareActionUsesTheCurrentValidatedRequest(int width, bool dark)
	{
		using var key = new NBitcoin.Key(Enumerable.Repeat((byte)0x42, 32).ToArray());
		var address = key.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.RegTest).ToString();
		var share = new MobileShareRequestTests.ShareSpy();
		using var state = new MobileReceivePresentation(address,
			text => Observable.Return(new QrEncoder().Encode(text).Matrix.InternalArray),
			_ => Task.CompletedTask, ImmediateScheduler.Instance, share);
		var surface = new MobileReceiveSurface { DataContext = state };
		using var errors = new MobileScreenshot.BindingErrors();
		var window = new Window
		{
			Width = width, Height = 844, SystemDecorations = SystemDecorations.None,
			RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
			Content = new MobilePage { Header = "Receive Bitcoin", ShowBack = true,
				Content = new ScrollViewer { Content = new Border { Padding = new Thickness(16,8,16,16), Child = surface } } }
		};
		try
		{
			window.Show(); Pump();
			var button = surface.FindControl<Button>("ShareReceive")!;
			Assert.True(button.IsVisible);
			Assert.Equal(1, Grid.GetColumnSpan(surface.FindControl<Button>("CopyReceive")!));
			foreach (var request in new[] { false, true })
			{
				state.Amount = request ? "0.015" : ""; Pump();
				MobileScreenshot.Capture(window, $"receive-sharing-{(request ? "amount" : "address")}-{width}-{(dark ? "dark" : "light")}",
					"receive-sharing", "bound-regtest-share-boundary");
				Press(window, button);
				Assert.Equal(state.PaymentRequest, share.Payloads[^1]);
			}
			state.Amount = "0.000000001"; Pump();
			Assert.False(button.IsEffectivelyEnabled);
			Assert.Equal(2, share.Payloads.Count);
			state.SelectAddressCommand.Execute(null); Pump();
			Assert.True(button.IsEffectivelyEnabled);
			Assert.Equal(address, state.PaymentRequest);
			share.Handler = (_, _) => throw new InvalidOperationException("fixture unavailable");
			Press(window, button);
			Assert.True(surface.FindControl<TextBlock>("ShareError")!.IsVisible);
			Assert.True(button.IsEffectivelyEnabled);
			MobileScreenshot.AssertNoHorizontalOverflow(window);
			Assert.Empty(errors.Errors);
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void UnsupportedHostDoesNotDisplayANonfunctionalShareAction()
	{
		using var state = new MobileReceivePresentation("wallet-validated-address",
			_ => Observable.Return(new bool[21, 21]), _ => Task.CompletedTask, ImmediateScheduler.Instance);
		var surface = new MobileReceiveSurface { DataContext = state };
		var window = new Window { Width = 390, Height = 844, Content = surface };
		try
		{
			window.Show(); Pump();
			Assert.False(surface.FindControl<Button>("ShareReceive")!.IsVisible);
			Assert.Equal(2, Grid.GetColumnSpan(surface.FindControl<Button>("CopyReceive")!));
		}
		finally { window.Close(); }
	}
	private static void Press(Window window, Control control)
	{
		control.BringIntoView(); Pump(); Assert.True(control.Focus());
		window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
		window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None); Pump();
	}
	private static void Pump() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
}
