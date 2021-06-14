using NBitcoin;
using System.Linq;
using WalletWasabi.WabiSabi.Client;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client
{
	public class GreedyDecomposerTests
	{
		[Theory]
		[InlineData(0, new long[] { })]
		[InlineData(1, new long[] { 1 })]
		[InlineData(2, new long[] { 2 })]
		[InlineData(3, new long[] { 2, 1 })]
		[InlineData(4, new long[] { 2, 2 })]
		[InlineData(5, new long[] { 5 })]
		[InlineData(6, new long[] { 5, 1 })]
		[InlineData(7, new long[] { 5, 2 })]
		[InlineData(8, new long[] { 5, 2, 1 })]
		[InlineData(9, new long[] { 5, 2, 2 })]
		[InlineData(10, new long[] { 10 })]
		[InlineData(11, new long[] { 10, 1 })]
		[InlineData(12, new long[] { 10, 2 })]
		[InlineData(13, new long[] { 10, 2, 1 })]
		[InlineData(14, new long[] { 10, 2, 2 })]
		[InlineData(15, new long[] { 10, 5 })]
		[InlineData(16, new long[] { 10, 5, 1 })]
		[InlineData(17, new long[] { 10, 5, 2 })]
		[InlineData(18, new long[] { 10, 5, 2, 1 })]
		[InlineData(19, new long[] { 10, 5, 2, 2 })]
		[InlineData(38, new long[] { 10, 10, 10, 5, 2, 1 })]
		public void Decompose1Test(long amount, long[] expected)
		{
			var decomposer = new GreedyDecomposer(new Money[] { new(1L), new(2L), new(5L), new(10L) });
			var amounts = decomposer.Decompose(Money.Satoshis(amount), Money.Zero);
			Assert.Equal(Money.Satoshis(amount), amounts.Sum());
			Assert.Equal(expected, amounts.Select(x => x.Satoshi));
		}

		[Theory]
		[InlineData(0, new long[] { })]
		[InlineData(1, new long[] { 1 })]
		[InlineData(2, new long[] { 1, 1 })]
		[InlineData(3, new long[] { 3 })]
		[InlineData(4, new long[] { 3, 1 })]
		[InlineData(5, new long[] { 3, 1, 1 })]
		[InlineData(6, new long[] { 3, 3 })]
		[InlineData(7, new long[] { 3, 3, 1 })]
		[InlineData(8, new long[] { 3, 3, 1, 1 })]
		[InlineData(9, new long[] { 3, 3, 3 })]
		[InlineData(10, new long[] { 3, 3, 3, 1 })]
		[InlineData(11, new long[] { 11 })]
		[InlineData(12, new long[] { 11, 1 })]
		[InlineData(13, new long[] { 11, 1, 1 })]
		[InlineData(14, new long[] { 11, 3 })]
		[InlineData(15, new long[] { 11, 3, 1 })]
		[InlineData(16, new long[] { 11, 3, 1, 1 })]
		[InlineData(17, new long[] { 11, 3, 3 })]
		[InlineData(18, new long[] { 11, 3, 3, 1 })]
		[InlineData(19, new long[] { 11, 3, 3, 1, 1 })]
		[InlineData(38, new long[] { 11, 11, 11, 3, 1, 1 })]
		public void Decompose2Test(long amount, long[] expected)
		{
			var decomposer = new GreedyDecomposer(new Money[] { new(1L), new(3L), new(11L) });
			var amounts = decomposer.Decompose(Money.Satoshis(amount), Money.Zero);
			Assert.Equal(Money.Satoshis(amount), amounts.Sum());
			Assert.Equal(expected, amounts.Select(x => x.Satoshi));
		}

		[Theory]
		[InlineData(0, new long[] { })]
		[InlineData(1, new long[] { })]
		[InlineData(2, new long[] { 1 })]
		[InlineData(3, new long[] { 1 })]
		[InlineData(4, new long[] { 3 })]
		[InlineData(5, new long[] { 3 })]
		[InlineData(6, new long[] { 3, 1 })]
		[InlineData(7, new long[] { 3, 1 })]
		[InlineData(8, new long[] { 3, 3 })]
		[InlineData(9, new long[] { 3, 3 })]
		[InlineData(10, new long[] { 3, 3, 1 })]
		[InlineData(11, new long[] { 3, 3, 1 })]
		[InlineData(12, new long[] { 11 })]
		[InlineData(13, new long[] { 11 })]
		[InlineData(14, new long[] { 11, 1 })]
		[InlineData(15, new long[] { 11, 1 })]
		[InlineData(16, new long[] { 11, 3 })]
		[InlineData(17, new long[] { 11, 3 })]
		[InlineData(18, new long[] { 11, 3, 1 })]
		[InlineData(19, new long[] { 11, 3, 1 })]
		[InlineData(38, new long[] { 11, 11, 11, 1 })]
		public void DecomposeWithFee(long amount, long[] expected)
		{
			var decomposer = new GreedyDecomposer(new Money[] { new(1L), new(3L), new(11L) });
			var amounts = decomposer.Decompose(Money.Satoshis(amount), new Money(1L));
			Assert.Equal(expected, amounts.Select(x => x.Satoshi));
		}
	}
}
