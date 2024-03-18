using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Helpers;

public class TransactionFeeHelperTests
{
	[Fact]
	public void GivenValidBlockTargetShouldReturnConfirmationTime()
	{
		var blockTarget = 1;
		var result = TransactionFeeHelper.CalculateConfirmationTime(blockTarget);
		Assert.Equal(10, result.Minutes);
	}

	[Fact]
	public void GivenNegativeBlockTargetShouldReturnError()
	{
		var blockTarget = -1;
		Assert.Throws<InvalidOperationException>(() => TransactionFeeHelper.CalculateConfirmationTime(blockTarget));
	}

	[Fact]
	public void GivenZeroBlockTargetShouldReturnError()
	{
		var blockTarget = 0;
		Assert.Throws<InvalidOperationException>(() => TransactionFeeHelper.CalculateConfirmationTime(blockTarget));
	}
}