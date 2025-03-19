using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Moq;
using NBitcoin;
using WalletWasabi.Fluent.Models.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class AmountTests
{
	[Fact]
	public void BtcShouldMatch()
	{
		var money = Money.FromUnit(1, MoneyUnit.BTC);
		var btcAmount = new Amount(money, Mock.Of<IAmountProvider>(provider => provider.BtcToUsdExchangeRate == Observable.Empty<decimal>()));
		Assert.Equal(money, btcAmount.Btc);
	}

	[Fact]
	public void UsdValueShouldChangeWithEachExchangeRate()
	{
		// ARRANGE
		var exchangeRates = new[] { 1m, 2m, 3m };
		var money = Money.FromUnit(2, MoneyUnit.BTC);
		using var rates = new Subject<decimal>();
		var exchangeProvider = Mock.Of<IAmountProvider>(x => x.BtcToUsdExchangeRate == rates);
		var destination = new List<decimal>();
		var sut = new Amount(money, exchangeProvider);
		using var usdValues = sut.Usd.Dump(destination);

		// ACT
		rates.Inject(exchangeRates);

		// ASSERT
		Assert.Equal([0, 2, 4, 6], destination);
	}
}
