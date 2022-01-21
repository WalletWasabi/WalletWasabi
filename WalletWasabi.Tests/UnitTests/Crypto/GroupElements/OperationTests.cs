using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.GroupElements;

public class OperationTests
{
	[Fact]
	public void Addition()
	{
		var gen1 = GroupElement.Infinity + Generators.G;
		var gen2 = Generators.G + GroupElement.Infinity;
		var inf = GroupElement.Infinity + GroupElement.Infinity;
		Assert.Equal(Generators.G, gen1);
		Assert.Equal(Generators.G, gen2);
		Assert.Equal(GroupElement.Infinity, inf);

		var one = new GroupElement(EC.G) * new Scalar(1);
		var two = new GroupElement(EC.G) * new Scalar(2);
		var three = new GroupElement(EC.G) * new Scalar(3);
		var zero = new GroupElement(EC.G) * Scalar.Zero;

		Assert.Equal(Generators.G, one);
		Assert.True(zero.IsInfinity);

		Assert.Equal(two, one + one);
		Assert.Equal(three, one + one + one);
		Assert.Equal(three, two + one);
		Assert.Equal(three, one + two);
		Assert.Equal(one, one + zero);
		Assert.Equal(two, one + one + zero);
		Assert.Equal(two, two + zero);
		Assert.Equal(three, three + zero);
		Assert.Equal(three, two + one + zero);
	}

	[Fact]
	public void Subtraction()
	{
		var minusGen = GroupElement.Infinity - Generators.G;
		var gen = Generators.G - GroupElement.Infinity;
		var inf = GroupElement.Infinity - GroupElement.Infinity;
		Assert.Equal(new GroupElement(EC.G.Negate()), minusGen);
		Assert.Equal(Generators.G, gen);
		Assert.Equal(GroupElement.Infinity, inf);

		var minusOne = new GroupElement(EC.G.Negate()) * new Scalar(1);
		var minusTwo = new GroupElement(EC.G.Negate()) * new Scalar(2);
		var one = new GroupElement(EC.G) * new Scalar(1);
		var two = new GroupElement(EC.G) * new Scalar(2);
		var three = new GroupElement(EC.G) * new Scalar(3);
		var zero = new GroupElement(EC.G) * Scalar.Zero;

		Assert.Equal(Generators.G, one);
		Assert.True(zero.IsInfinity);

		Assert.Equal(zero, one - one);
		Assert.Equal(minusOne, one - one - one);
		Assert.Equal(one, one + one - one);
		Assert.Equal(one, one - one + one);
		Assert.Equal(one, two - one);
		Assert.Equal(minusOne, one - two);
		Assert.Equal(one, one - zero);
		Assert.Equal(minusOne, zero - one);
		Assert.Equal(zero, one - one + zero);
		Assert.Equal(two, one + one - zero);
		Assert.Equal(zero, one - one - zero);
		Assert.Equal(two, two - zero);
		Assert.Equal(minusTwo, zero - two);
		Assert.Equal(three, three - zero);
		Assert.Equal(two, three - one);
		Assert.Equal(minusTwo, one - three);
		Assert.Equal(one, two - one + zero);
		Assert.Equal(zero, zero + zero - zero + zero);
	}

	[Fact]
	public void Negation()
	{
		Assert.Equal(new GroupElement(EC.G.Negate()), Generators.G.Negate());
		Scalar one = new(1);
		Assert.Equal(new GroupElement(EC.G.Negate()) * one, (new GroupElement(EC.G) * one).Negate());
		Assert.Equal(GroupElement.Infinity, GroupElement.Infinity.Negate());
	}

	[Fact]
	public void MultiplyByScalar()
	{
		// Scalar one.
		var g = Generators.G;
		var scalar = Scalar.One;
		var expected = new GroupElement(EC.G) * scalar;
		Assert.Equal(expected, g * scalar);

		// Can switch order.
		Assert.Equal(expected, scalar * g);

		// Scalar two.
		scalar = new Scalar(2);
		expected = new GroupElement(EC.G) * scalar;
		Assert.Equal(expected, g * scalar);

		// Scalar three.
		scalar = new Scalar(3);
		expected = new GroupElement(EC.G) * scalar;
		Assert.Equal(expected, g * scalar);

		// Scalar NC.
		scalar = EC.NC;
		expected = new GroupElement(EC.G) * scalar;
		Assert.Equal(expected, g * scalar);

		// Scalar big.
		scalar = new Scalar(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
		expected = new GroupElement(EC.G) * scalar;
		Assert.Equal(expected, g * scalar);

		// Scalar biggest.
		scalar = EC.N + Scalar.One.Negate();
		expected = new GroupElement(EC.G) * scalar;
		Assert.Equal(expected, g * scalar);

		// Scalar zero.
		scalar = Scalar.Zero;
		expected = new GroupElement(EC.G) * scalar;
		var result = g * scalar;
		Assert.Equal(expected, result);
		Assert.True(result.IsInfinity);

		// Group element is infinity.
		scalar = new Scalar(2);
		result = GroupElement.Infinity * scalar;
		expected = GroupElement.Infinity;
		Assert.Equal(expected, result);
		Assert.True(result.IsInfinity);

		// Group element is infinity & Scalar is zero.
		scalar = Scalar.Zero;
		result = GroupElement.Infinity * scalar;
		expected = GroupElement.Infinity;
		Assert.Equal(expected, result);
		Assert.True(result.IsInfinity);
	}

	[Fact]
	public void OverflownScalar()
	{
		var g = Generators.G;

		// Scalar overflown N.
		var scalar = EC.N;
		var expected = new GroupElement(EC.G) * scalar;
		Assert.Equal(expected, g * scalar);
		Assert.Equal(g * Scalar.Zero, g * scalar);

		// Scalar overflown N+1.
		scalar = EC.N + Scalar.One;
		expected = new GroupElement(EC.G) * scalar;
		Assert.Equal(expected, g * scalar);
		Assert.Equal(g * Scalar.One, g * scalar);

		// Scalar overflown uint.Max
		scalar = new Scalar(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);
		expected = new GroupElement(EC.G) * scalar;
		Assert.Equal(expected, g * scalar);
	}
}
