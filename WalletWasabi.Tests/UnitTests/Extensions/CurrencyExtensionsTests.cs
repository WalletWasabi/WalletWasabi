using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Wallets.Exchange;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Extensions;

public class CurrencyExtensionsTests
{
	[Fact]
	public void BtcToFiatKeepsTheCurrencyOfTheRate()
	{
		var fiat = Money.Coins(0.5m).BtcToFiat(new ExchangeRate("JPY", 12646480m));

		Assert.Equal(new FiatAmount(6323240m, "JPY"), fiat);
	}

	[Theory]
	[InlineData(72009.4, "EUR", "72 010 EUR")]
	[InlineData(1.234, "GBP", "1.2 GBP")]
	[InlineData(0.567, "CHF", "0.57 CHF")]
	public void FormattedFiatEndsWithTheCurrency(decimal value, string currency, string expected)
	{
		Assert.Equal(expected, new FiatAmount(value, currency).ToFiatFormatted());
	}

	[Fact]
	public void ApproximationsAreEmptyForZero()
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
