using NBitcoin;
using WalletWasabi.Client.Wallets.Exchange;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Extensions;

/// <summary>
/// Tests for <see cref="CurrencyExtensions"/>.
/// </summary>
public class CurrencyExtensionsTests
{
	/// <summary>
	/// Tests that the conversion from BTC to fiat currency is correct for various scenarios.
	/// </summary>
	[Theory]
	[InlineData(1.0, "USD", 80_000, 80_000)]
	[InlineData(0.5, "USD", 80_000, 40_000)]
	[InlineData(0, "USD", 80_000, 0)] // No BTC, no fiat.
	[InlineData(2, "EUR", 70_000, 140_000)]
	[InlineData(0.25, "EUR", 60_000, 15_000)]
	[InlineData(0.5, "JPY", 12_646_480, 6_323_240)]
	public void BtcToFiat(double btc, string currency, double rate, double expectedFiat)
	{
		var actualFiatAmount = Money.Coins((decimal)btc).BtcToFiat(new ExchangeRate(currency, (decimal)rate));
		var expectedFiatAmount = new FiatAmount((decimal)expectedFiat, currency);

		Assert.Equal(expectedFiatAmount, actualFiatAmount);
	}

	/// <summary>
	/// Expected values ends with the currency symbol, and the value is formatted with spaces as thousands separators.
	/// </summary>
	[Theory]
	[InlineData(72009.4, "EUR", "72 010 EUR")]
	[InlineData(1.234, "GBP", "1.2 GBP")]
	[InlineData(0.567, "CHF", "0.57 CHF")]
	public void ToFiatFormatted(decimal value, string currency, string expected)
	{
		Assert.Equal(expected, new FiatAmount(value, currency).ToFiatFormatted());
	}

	[Fact]
	public void ToFiatApprox()
	{
		var zero = new FiatAmount(0m, "EUR");

		Assert.Equal("", zero.ToFiatAprox());
		Assert.Equal("", zero.ToFiatAproxBetweenParens());
		Assert.Equal("(≈72 010 EUR)", new FiatAmount(72009.4m, "EUR").ToFiatAproxBetweenParens());
	}

	[Fact]
	public void AbsDropsTheSign()
	{
		Assert.Equal(new FiatAmount(3m, "CAD"), new FiatAmount(-3m, "CAD").Abs());
	}
}
