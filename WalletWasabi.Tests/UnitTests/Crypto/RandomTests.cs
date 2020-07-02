using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.WabiSabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class RandomTests
	{
		[Fact]
		public void ScalarTests()
		{
			// Make sure first that scalar equality works within hashset and that the underlying API won't pull the floor out.
			var singleSet = new HashSet<Scalar>();
			var scalar = new Scalar(5);
			var same = new Scalar(5);
			singleSet.Add(scalar);
			singleSet.Add(same);
			Assert.Single(singleSet);

			// It's probabilistically ensured that it never produces the same scalar, so unit test should pass always.
			var nonZeroSet = new HashSet<Scalar>();
			var count = 100;
			for (int i = 0; i < count; i++)
			{
				Scalar random = SecureRandom.GetScalarNonZero();
				nonZeroSet.Add(random);
			}
			Assert.Equal(count, nonZeroSet.Count);

			// Well, this is unlikely to catch any issues, but it catches the large ones at least.
			Assert.True(nonZeroSet.All(x => x != Scalar.Zero));
		}
	}
}
