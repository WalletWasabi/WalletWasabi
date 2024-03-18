using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Helpers;

public class TransactionFeeHelperTests
{
	[Fact]
	public void ShouldReturnConfirmationTimeWhenReceiveAValidBlockTarget()
	{
		var blockTarget = 1.0;
		var result = TransactionFeeHelper.CalculateConfirmationTime(blockTarget);
		Assert.Equal(10, result.Minutes);
	}

	[Fact]
	public void ShouldReturnErrorWhenReceiveANegativeBlockTarget()
	{
		var blockTarget = -1.0;
		Assert.Throws<InvalidOperationException>(() => TransactionFeeHelper.CalculateConfirmationTime(blockTarget));
	}

	[Fact]
	public void ShouldReturnErrorWhenReceiveZeroBlockTarget()
	{
		var blockTarget = 0.0;
		Assert.Throws<InvalidOperationException>(() => TransactionFeeHelper.CalculateConfirmationTime(blockTarget));
	}
}