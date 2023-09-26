using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Moq;
using NBitcoin;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.ViewModels.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class BitcoinAmountTests
{
	[Fact]
	public void Value()
	{
		var money = Money.FromUnit(1, MoneyUnit.BTC);
		var btcAmount = new BtcAmount(money, null);
		Assert.Equal(money, btcAmount.Value);
	}

	[Fact]
	public void UsdValueShouldChangeWithEachExchangeRate()
	{
		// ARRANGE
		var exchangeRates = new[] { 1m, 2m, 3m };
		var money = Money.FromUnit(2, MoneyUnit.BTC);
		using var rates = new Subject<decimal>();
		var exchangeProvider = Mock.Of<IExchangeRateProvider>(x => x.BtcToUsdRate == rates);
		var destination = new List<decimal>();
		var sut = new BtcAmount(money, exchangeProvider);
		using var usdValues = sut.UsdValue.Dump(destination);

		// ACT
		rates.Inject(exchangeRates);

		// ASSERT
		Assert.Equal(exchangeRates.Select(rate => rate * sut.Value.ToDecimal(MoneyUnit.BTC)), destination);
	}
}
