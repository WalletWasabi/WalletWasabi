using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Reactive.Testing;
using Moq;
using NBitcoin;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Interfaces;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class WalletBalancesTests
{
	[Fact]
	public void Correct_balance_is_shown()
	{
		var ts = new TestScheduler();

		var balances = new Subject<Money>();
		var rates = new Subject<decimal>();

		var sut = new WalletBalancesModel(Mock.Of<IWalletModel>(x => x.Balance == balances), Mock.Of<IObservableExchangeRateProvider>(x => x.BtcToUsdRate == rates));

		var observer = ts.CreateObserver<decimal>();
		sut.UsdBalance.Subscribe(observer);

		balances.OnNext(Money.Coins(2));
		rates.OnNext(new decimal(0.5));
	}
}
