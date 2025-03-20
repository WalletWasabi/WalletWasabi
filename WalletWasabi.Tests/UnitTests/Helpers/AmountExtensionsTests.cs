using System.Reactive.Linq;
using Moq;
using NBitcoin;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Helpers;

public class AmountExtensionsTests
{
	[Fact]
	public void DifferenceShouldBeExpected()
	{
		var exchangeRateProvider = Mock.Of<IAmountProvider>(x => x.BtcToUsdExchangeRate == Observable.Return(1m));
		var previous = new Amount(Money.FromUnit(221, MoneyUnit.Satoshi), exchangeRateProvider);
		var current = new Amount(Money.FromUnit(110, MoneyUnit.Satoshi), exchangeRateProvider);

		var result = current.Diff(previous);

		var expected = -0.5m;
		decimal tolerance = 0.01m;
		var areApproximatelyEqual = Math.Abs((decimal)result - expected) < tolerance;
		Assert.True(areApproximatelyEqual, $"Result is not the expected by the given tolerance. Result: {result}, Expected: {expected}, Tolerance: {tolerance}");
	}
}
