using NBitcoin;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.Rounds;

public class MaxSuggestedAmountProviderTests
{
	[Fact]
	public void Constructor_InitializesMaxSuggestedAmountCorrectly()
	{
		var initialMax = Money.Satoshis(100_000);
		var maxRegistrable = Money.Satoshis(1_000_000);

		var provider = new MaxSuggestedAmountProvider(initialMax, maxRegistrable);

		Assert.NotNull(provider.MaxSuggestedAmount);
		Assert.True(provider.MaxSuggestedAmount <= maxRegistrable);
	}

	[Fact]
	public void StepMaxSuggested_FailedRegistration_SetsMaxToMaxRegistrable()
	{
		var initialMax = Money.Satoshis(100000);
		var maxRegistrable = Money.Satoshis(1_000_000);
		var provider = new MaxSuggestedAmountProvider(initialMax, maxRegistrable);

		provider.ResetMaxSuggested();

		Assert.Equal(maxRegistrable, provider.MaxSuggestedAmount);
	}

	[Fact]
	public void StepMaxSuggested_SuccessfulRegistration_DecreasesMaxSuggestedAmount()
	{
		var initialMax = Money.Satoshis(100000);
		var maxRegistrable = Money.Satoshis(1000000);
		var provider = new MaxSuggestedAmountProvider(initialMax, maxRegistrable);
		var originalMax = provider.MaxSuggestedAmount;

		provider.StepMaxSuggested();

		Assert.True(provider.MaxSuggestedAmount <= originalMax);
	}

	[Fact]
	public void StepMaxSuggested_MultipleSuccessfulCalls_AmountPatter()
	{
		var initialMax = Money.Satoshis(100_000);
		var maxRegistrable = Money.Satoshis(1_000_000_000);
		var provider = new MaxSuggestedAmountProvider(initialMax, maxRegistrable);

		var amounts = new Money[10];

		for (int i = 0; i < 8; i++)
		{
			provider.StepMaxSuggested();
			amounts[i] = provider.MaxSuggestedAmount;
		}

		Assert.Equal(Money.Satoshis(      100_000), amounts[0]);
		Assert.Equal(Money.Satoshis(    1_000_000), amounts[1]);
		Assert.Equal(Money.Satoshis(      100_000), amounts[2]);
		Assert.Equal(Money.Satoshis(   10_000_000), amounts[3]);
		Assert.Equal(Money.Satoshis(      100_000), amounts[4]);
		Assert.Equal(Money.Satoshis(    1_000_000), amounts[5]);
		Assert.Equal(Money.Satoshis(      100_000), amounts[6]);
		Assert.Equal(Money.Satoshis(  100_000_000), amounts[7]);
	}

	[Fact]
	public void StepMaxSuggested_FailureAfterSuccess_ResetsToMaxRegistrable()
	{
		var initialMax = Money.Satoshis(100000);
		var maxRegistrable = Money.Satoshis(1000000);
		var provider = new MaxSuggestedAmountProvider(initialMax, maxRegistrable);

		provider.StepMaxSuggested();
		var reducedAmount = provider.MaxSuggestedAmount;
		Assert.True(reducedAmount < maxRegistrable);

		provider.ResetMaxSuggested();

		Assert.Equal(maxRegistrable, provider.MaxSuggestedAmount);
	}

	[Theory]
	[InlineData(1000, 10000)]
	[InlineData(50000, 500000)]
	[InlineData(100000, 1000000)]
	public void StepMaxSuggested_AmountAlwaysWithinBounds(long initialMax, long maxRegistrable)
	{
		var provider = new MaxSuggestedAmountProvider(
			Money.Satoshis(initialMax),
			Money.Satoshis(maxRegistrable)
		);

		for (int i = 0; i < 20; i++)
		{
			if (i % 2 == 0)
			{
				provider.StepMaxSuggested();
			}
			else
			{
				provider.ResetMaxSuggested();
			}

			Assert.True(provider.MaxSuggestedAmount >= Money.Zero);
			Assert.True(provider.MaxSuggestedAmount <= Money.Satoshis(maxRegistrable));
		}
	}

	[Fact]
	public void StepMaxSuggested_SequenceOfOperations_MaintainsInvariants()
	{
		var initialMax = Money.Satoshis(100000);
		var maxRegistrable = Money.Satoshis(1000000);
		var provider = new MaxSuggestedAmountProvider(initialMax, maxRegistrable);

		// Success, success, failure, success, success
		var operations = new[] { true, true, false, true, true };

		foreach (var isSuccess in operations)
		{
			if (isSuccess)
			{
				provider.StepMaxSuggested();
			}
			else
			{
				provider.ResetMaxSuggested();
			}
			Assert.True(provider.MaxSuggestedAmount <= maxRegistrable);
			Assert.True(provider.MaxSuggestedAmount >= Money.Zero);
		}
	}
}
