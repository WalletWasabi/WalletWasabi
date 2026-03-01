using System.Collections.Generic;
using System.Reactive.Subjects;
using Moq;
using NBitcoin;
using WalletWasabi.Daemon;
using WalletWasabi.Fluent;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class AmountTests
{
	[Fact]
	public void BtcShouldMatch()
	{
		var eventBus = new EventBus();
		WalletWasabi.Fluent.Services.EventBus = eventBus;
		WalletWasabi.Fluent.Services.Status = new StatusContainer(eventBus);
		eventBus.Publish(new ExchangeRateChanged(88_000));

		var money = Money.FromUnit(1, MoneyUnit.BTC);
		var btcAmount = new Amount(money, new AmountProvider());
		Assert.Equal(money, btcAmount.Btc);
	}

	[Fact]
	public void UsdValueShouldChangeWithEachExchangeRate()
	{
		// ARRANGE
		var eventBus = new EventBus();
		WalletWasabi.Fluent.Services.EventBus = eventBus;
		WalletWasabi.Fluent.Services.Status = new StatusContainer(eventBus);

		var money = Money.FromUnit(2, MoneyUnit.BTC);
		var destination = new List<decimal>();
		var sut = new Amount(money, new AmountProvider());
		using var usdValues = sut.Usd.Dump(destination);

		// ACT
		eventBus.Publish(new ExchangeRateChanged(1));
		eventBus.Publish(new ExchangeRateChanged(2));
		eventBus.Publish(new ExchangeRateChanged(3));

		// ASSERT
		Assert.Equal([0, 2, 4, 6], destination);
	}
}
