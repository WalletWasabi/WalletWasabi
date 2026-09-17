using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NBitcoin;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileFeeRatePresentationTests
{
	[Fact]
	public void MissingRateIsNotDisplayedAsAZeroFee()
	{
		var converter = new MobileFeeRateConverter();
		Assert.Equal("Fee rate unavailable", converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
		Assert.Equal("Fee rate: 1.25 sat/vB", converter.Convert(new FeeRate(1.25m), typeof(string), null, CultureInfo.InvariantCulture));
	}

	[AvaloniaFact]
	public void ReviewFeeRateCanArriveAndDisappearWithoutBindingErrors()
	{
		using var errors = new MobileScreenshot.BindingErrors();
		var surface = new MobileReviewSurface();
		var window = new Window { Width = 390, Height = 844, Content = surface };
		try
		{
			window.Show(); Dispatcher.UIThread.RunJobs();
			var label = surface.FindControl<TextBlock>("FeeRateLabel")!;
			Assert.Equal("Fee rate unavailable", label.Text);
			surface.FeeRate = new FeeRate(2.5m); Dispatcher.UIThread.RunJobs();
			Assert.Equal("Fee rate: 2.5 sat/vB", label.Text);
			surface.FeeRate = null; Dispatcher.UIThread.RunJobs();
			Assert.Equal("Fee rate unavailable", label.Text);
			Assert.Empty(errors.Errors);
		}
		finally { window.Close(); }
	}
}
