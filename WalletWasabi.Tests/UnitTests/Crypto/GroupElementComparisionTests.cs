using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class GroupElementComparisionTests
	{
		[Fact]
		public void OneOneEqualsOneOne()
		{
			var one = new FE(1);
			var a = new GE(one, one);
			var b = new GE(one, one);
			Assert.True(Secp256k1Helpers.Equals(a, b));
		}

		[Fact]
		public void TwoOneDoesntEqualOneOne()
		{
			var one = new FE(1);
			var two = new FE(2);
			var a = new GE(one, one);
			var b = new GE(two, one);
			Assert.False(Secp256k1Helpers.Equals(a, b));
		}

		[Fact]
		public void InfinityDoesntEqualNotInfinity()
		{
			var one = new FE(1);
			var a = new GE(one, one);
			var b = new GE(one, one, infinity: true);
			Assert.False(Secp256k1Helpers.Equals(a, b));
		}

		[Fact]
		public void InfinityDoesntCareCoordinates()
		{
			var one = new FE(1);
			var two = new FE(2);
			var a = new GE(one, one, infinity: true);
			var b = new GE(two, one, infinity: true);
			Assert.True(Secp256k1Helpers.Equals(a, b));
		}

		[Fact]
		public void MagnitudeDoesntmatter()
		{
			// ToDo: is this ok?
			var one = new FE(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, magnitude: 0, normalized: false);
			var one2 = new FE(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, magnitude: 1, normalized: false);
			var a = new GE(one, one);
			var b = new GE(one2, one);
			Assert.True(Secp256k1Helpers.Equals(a, b));

			one = new FE(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, magnitude: 0, normalized: false);
			one2 = new FE(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, magnitude: 2, normalized: false);
			a = new GE(one, one);
			b = new GE(one2, one);
			Assert.True(Secp256k1Helpers.Equals(a, b));

			one = new FE(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, magnitude: 1, normalized: false);
			one2 = new FE(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, magnitude: 2, normalized: false);
			a = new GE(one, one);
			b = new GE(one2, one);
			Assert.True(Secp256k1Helpers.Equals(a, b));
		}

		[Fact]
		public void NormalizationDoesntmatter()
		{
			// ToDo: is this ok?
			var one = new FE(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, magnitude: 0, normalized: false);
			var one2 = new FE(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, magnitude: 0, normalized: true);
			var a = new GE(one, one);
			var b = new GE(one2, one);
			Assert.True(Secp256k1Helpers.Equals(a, b));
		}
	}
}
