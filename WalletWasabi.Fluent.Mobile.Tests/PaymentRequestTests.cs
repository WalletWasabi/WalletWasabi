using System.Globalization;
using WalletWasabi.Fluent.Mobile.Controls;
using WalletWasabi.Fluent.Mobile.ViewModels;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class PaymentRequestTests
{
	// Parser contract: this address has already been generated and validated by the wallet.
	private const string Address = "wallet-generated-address";

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void EmptyAmountKeepsAddressOnly(string? amount)
	{
		Assert.True(MobileReceiveRequestViewModel.TryCreateRequest(Address, amount, out var request));
		Assert.Equal(Address, request);
	}

	[Theory]
	[InlineData("0.00000001", "0.00000001")]
	[InlineData("1", "1")]
	[InlineData("0.01000000", "0.01")]
	[InlineData(" 0.1 ", "0.1")]
	[InlineData("21000000", "21000000")]
	[InlineData("20999999.99999999", "20999999.99999999")]
	[InlineData("0001.25000000", "1.25")]
	[InlineData(".5", "0.5")]
	public void RequestsUseExactInvariantBtc(string amount, string normalized)
	{
		Assert.True(MobileReceiveRequestViewModel.TryCreateRequest(Address, amount, out var request));
		Assert.Equal($"bitcoin:{Address}?amount={normalized}", request);
		Assert.DoesNotContain("label=", request);
	}

	[Theory]
	[InlineData("0")]
	[InlineData("0.00000000")]
	[InlineData("-1")]
	[InlineData("+1")]
	[InlineData("1e-8")]
	[InlineData("1,5")]
	[InlineData("1,000")]
	[InlineData("1 000")]
	[InlineData("1_000")]
	[InlineData("0.000000001")]
	[InlineData("1.000000000")]
	[InlineData("21000000.00000001")]
	[InlineData("999999999999999999999999999999999999999")]
	[InlineData("NaN")]
	[InlineData("Infinity")]
	[InlineData("1..2")]
	[InlineData(".")]
	[InlineData("1 BTC")]
	[InlineData("١")]
	public void InvalidAmountNeverProducesPaymentUri(string amount)
	{
		Assert.False(MobileReceiveRequestViewModel.TryCreateRequest(Address, amount, out _));
	}

	[Fact]
	public void MissingWalletAddressIsRejected()
	{
		Assert.Throws<ArgumentException>(() => MobileReceiveRequestViewModel.TryCreateRequest("", "1", out _));
	}

	[Theory]
	[InlineData("home", "home", true)]
	[InlineData("home", "privacy", false)]
	[InlineData(2, "2", true)]
	[InlineData(2, "3", false)]
	[InlineData(null, "home", false)]
	[InlineData(null, null, false)]
	public void SectionSelectionHandlesStringAndNumericRoutes(object? section, object? parameter, bool expected)
	{
		var converter = new MobileSectionConverter();
		Assert.Equal(expected, Assert.IsType<bool>(converter.Convert(section, typeof(bool), parameter, CultureInfo.InvariantCulture)));
	}
}
