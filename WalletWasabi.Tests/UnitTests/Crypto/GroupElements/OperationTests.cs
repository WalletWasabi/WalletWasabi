using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.GroupElements
{
	public class OperationTests
	{
		[Fact]
		public void Addition()
		{
			var gen1 = GroupElement.Infinity + GroupElement.G;
			var gen2 = GroupElement.G + GroupElement.Infinity;
			var inf = GroupElement.Infinity + GroupElement.Infinity;
			Assert.Equal(GroupElement.G, gen1);
			Assert.Equal(GroupElement.G, gen2);
			Assert.Equal(GroupElement.Infinity, inf);

			var one = new GroupElement(new Scalar(1) * EC.G);
			var two = new GroupElement(new Scalar(2) * EC.G);
			var three = new GroupElement(new Scalar(3) * EC.G);
			var zero = new GroupElement(Scalar.Zero * EC.G);

			Assert.Equal(GroupElement.G, one);
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
			var minusGen = GroupElement.Infinity - GroupElement.G;
			var gen = GroupElement.G - GroupElement.Infinity;
			var inf = GroupElement.Infinity - GroupElement.Infinity;
			Assert.Equal(new GroupElement(EC.G.Negate()), minusGen);
			Assert.Equal(GroupElement.G, gen);
			Assert.Equal(GroupElement.Infinity, inf);

			var minusOne = new GroupElement(new Scalar(1) * EC.G.Negate());
			var minusTwo = new GroupElement(new Scalar(2) * EC.G.Negate());
			var one = new GroupElement(new Scalar(1) * EC.G);
			var two = new GroupElement(new Scalar(2) * EC.G);
			var three = new GroupElement(new Scalar(3) * EC.G);
			var zero = new GroupElement(Scalar.Zero * EC.G);

			Assert.Equal(GroupElement.G, one);
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
			Assert.Equal(new GroupElement(EC.G.Negate()), GroupElement.G.Negate());
			Scalar one = new Scalar(1);
			Assert.Equal(new GroupElement(EC.G.Negate() * one), new GroupElement(EC.G * one).Negate());
			Assert.Equal(GroupElement.Infinity, GroupElement.Infinity.Negate());
		}

		[Fact]
		public void MultiplyByScalar()
		{
			// Scalar one.
			var g = GroupElement.G;
			var scalar = Scalar.One;
			var expected = new GroupElement(EC.G * scalar);
			Assert.Equal(expected, g * scalar);

			// Can switch order.
			Assert.Equal(expected, scalar * g);

			// Scalar two.
			scalar = new Scalar(2);
			expected = new GroupElement(EC.G * scalar);
			Assert.Equal(expected, g * scalar);

			// Scalar three.
			scalar = new Scalar(3);
			expected = new GroupElement(EC.G * scalar);
			Assert.Equal(expected, g * scalar);

			// Scalar NC.
			scalar = EC.NC;
			expected = new GroupElement(EC.G * scalar);
			Assert.Equal(expected, g * scalar);

			// Scalar big.
			scalar = new Scalar(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
			expected = new GroupElement(EC.G * scalar);
			Assert.Equal(expected, g * scalar);

			// Scalar biggest.
			scalar = EC.N + Scalar.One.Negate();
			expected = new GroupElement(EC.G * scalar);
			Assert.Equal(expected, g * scalar);

			// Scalar zero.
			scalar = Scalar.Zero;
			expected = new GroupElement(EC.G * scalar);
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
			var g = GroupElement.G;

			// Scalar overflown N.
			var scalar = EC.N;
			var expected = new GroupElement(EC.G * scalar);
			Assert.Equal(expected, g * scalar);
			Assert.Equal(g * Scalar.Zero, g * scalar);

			// Scalar overflown N+1.
			scalar = EC.N + Scalar.One;
			expected = new GroupElement(EC.G * scalar);
			Assert.Equal(expected, g * scalar);
			Assert.Equal(g * Scalar.One, g * scalar);

			// Scalar overflown uint.Max
			scalar = new Scalar(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);
			expected = new GroupElement(EC.G * scalar);
			Assert.Equal(expected, g * scalar);
		}
	}
}
