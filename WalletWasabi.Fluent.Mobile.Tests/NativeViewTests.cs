using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class NativeViewTests
{
	public static IEnumerable<object[]> ViewCases()
	{
		foreach (var view in new[] { "wallet", "send", "receive", "address", "review", "fee", "settings", "shell" })
			foreach (var width in new[] { 320, 390 })
				foreach (var dark in new[] { false, true })
					yield return new object[] { view, width, dark };
	}

	[AvaloniaTheory]
	[MemberData(nameof(ViewCases))]
	public void NativeViewLoadsAndMeasuresWithoutStartingAWallet(string name, int width, bool dark)
	{
		Control view = name switch
		{
			"wallet" => new MobileWalletView(),
			"send" => new MobileSendView(),
			"receive" => new MobileReceiveView(),
			"address" => new MobileReceiveAddressView(),
			"review" => new MobileTransactionPreviewView(),
			"fee" => new MobileSendFeeView(),
			"settings" => new MobileSettingsView(),
			"shell" => new MobileShell(),
			_ => throw new ArgumentOutOfRangeException(nameof(name))
		};
		var window = new Window
		{
			Width = width,
			Height = 844,
			Content = view,
			RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
		};
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			Assert.True(double.IsFinite(view.DesiredSize.Width));
			Assert.True(double.IsFinite(view.DesiredSize.Height));
			Assert.True(view.Bounds.Width > 0);
			Assert.InRange(view.Bounds.Width, 0, width);
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void DynamicTokensUpdateExistingNativeControls()
	{
		var button = new Button { Content = "Receive" };
		button.Classes.Add("mobile-button");
		var window = new Window { Width = 390, Height = 844, Content = button, RequestedThemeVariant = ThemeVariant.Light };
		try
		{
			window.Show();
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(Color.Parse("#111B21"), Assert.IsAssignableFrom<ISolidColorBrush>(button.Foreground).Color);
			window.RequestedThemeVariant = ThemeVariant.Dark;
			Dispatcher.UIThread.RunJobs();
			Assert.Equal(Color.Parse("#F0F5F2"), Assert.IsAssignableFrom<ISolidColorBrush>(button.Foreground).Color);
		}
		finally { window.Close(); }
	}

	[AvaloniaFact]
	public void FooterAndHeaderActionsInheritThePageDataContext()
	{
		var model = new object();
		var footer = new Button();
		var header = new Button();
		var page = new MobilePage { DataContext = model, Footer = footer, HeaderActions = header };
		Assert.Same(model, footer.DataContext);
		Assert.Same(model, header.DataContext);
		var replacement = new Button();
		page.Footer = replacement;
		Assert.Same(model, replacement.DataContext);
		Assert.Null(footer.DataContext);
		var nextModel = new object();
		page.DataContext = nextModel;
		Assert.Same(nextModel, replacement.DataContext);
		Assert.Same(nextModel, header.DataContext);
	}

	[AvaloniaFact]
	public void NativeActionExposesItsAccessibleName()
	{
		var action = new MobileActionButton { Label = "Receive Bitcoin", Icon = "receive" };
		Assert.Equal("Receive Bitcoin", Avalonia.Automation.AutomationProperties.GetName(action));
		action.Label = "Send Bitcoin";
		Assert.Equal("Send Bitcoin", Avalonia.Automation.AutomationProperties.GetName(action));
	}

	[AvaloniaTheory]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	[InlineData(-1)]
	[InlineData(0)]
	[InlineData(100)]
	[InlineData(200)]
	public void PrivacyRingMeasuresWithBoundaryValues(double value)
	{
		var ring = new MobilePrivacyRing { Width = 196, Height = 196, Value = value, Foreground = Brushes.Green, Track = Brushes.Gray };
		ring.Measure(new Size(196, 196));
		ring.Arrange(new Rect(0, 0, 196, 196));
		Assert.Equal(new Size(196, 196), ring.Bounds.Size);
	}

	[AvaloniaFact]
	public void QrMatrixCanBeClearedWithoutRetainingThePreviousRequest()
	{
		var matrix = new bool[21, 21];
		matrix[0, 0] = true;
		var control = new MobileQrCode { Matrix = matrix };
		Assert.Same(matrix, control.Matrix);
		control.Matrix = null;
		Assert.Null(control.Matrix);
	}
}
