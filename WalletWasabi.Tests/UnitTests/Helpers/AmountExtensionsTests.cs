using NBitcoin;
using WalletWasabi.Daemon;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Helpers;

public class AmountExtensionsTests
{
	[Fact]
	public void DifferenceShouldBeExpected()
	{
		var eventBus = new EventBus();
		WalletWasabi.Fluent.Services.EventBus = eventBus;
		WalletWasabi.Fluent.Services.Status = new StatusContainer(eventBus);

		var p = new AmountProvider();
		var previous = new Amount(Money.FromUnit(221, MoneyUnit.Satoshi), p);
		var current = new Amount(Money.FromUnit(110, MoneyUnit.Satoshi), p);

		var result = current.Diff(previous);

		var expected = -0.5m;
		decimal tolerance = 0.01m;
		var areApproximatelyEqual = Math.Abs((decimal)result - expected) < tolerance;
		Assert.True(areApproximatelyEqual, $"Result is not the expected by the given tolerance. Result: {result}, Expected: {expected}, Tolerance: {tolerance}");
	}
}
