using NBitcoin.Secp256k1;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto;

public class ScalarTests
{
	[Fact]
	public void ScalarBehavesAsExpected()
	{
		// Just small scalar tests to showcase some edge cases how the Scalar behaves.
		Assert.True(EC.N.IsOverflow);

		var one = new Scalar(1);
		var nPlusOne = EC.N + one;
		Assert.False(nPlusOne.IsOverflow);
		Assert.Equal(one, nPlusOne);

		var nPlusOneMinusOne = nPlusOne + one.Negate();
		Assert.False(nPlusOne.IsOverflow);
		Assert.Equal(Scalar.Zero, nPlusOneMinusOne);

		var nMinusOne = EC.N + one.Negate();
		Assert.False(nMinusOne.IsOverflow);
		Assert.True(nMinusOne.IsHigh);

		var largest = new Scalar(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);
		Assert.True(largest.IsOverflow);

		var two = one + one;
		Assert.Equal(new Scalar(2), two);
		Assert.Equal(one, two + one.Negate());
		Assert.Equal(Scalar.Zero, two + one.Negate() + one.Negate());
	}
}
