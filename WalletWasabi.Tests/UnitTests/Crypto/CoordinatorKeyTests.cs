using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class CoordinatorKeyTests
	{
		[Fact]
		public void GenerateCoordinatorParameters()
		{
			// Coordinator key is (0, 0, 0, 0, 0)
			var rnd = new MockRandom();
			rnd.GetScalarResults.AddRange(Enumerable.Repeat(Scalar.Zero, 5));
			var key = new CoordinatorSecretKey(rnd);
			var ex = Assert.Throws<ArgumentException>(key.ComputeCoordinatorParameters);
			Assert.StartsWith("Point at infinity is not a valid value.", ex.Message);

			// Coordinator key is (0, 0, 1, 1, 1)
			rnd = new MockRandom();
			rnd.GetScalarResults.AddRange(Enumerable.Repeat(Scalar.Zero, 2));
			rnd.GetScalarResults.AddRange(Enumerable.Repeat(Scalar.One, 3));
			key = new CoordinatorSecretKey(rnd);
			ex = Assert.Throws<ArgumentException>(key.ComputeCoordinatorParameters);
			Assert.StartsWith("Point at infinity is not a valid value.", ex.Message);

			// Coordinator key is (1, 1, 0, 0, 0)
			rnd = new MockRandom();
			rnd.GetScalarResults.AddRange(Enumerable.Repeat(Scalar.One, 2));
			rnd.GetScalarResults.AddRange(Enumerable.Repeat(Scalar.Zero, 3));
			key = new CoordinatorSecretKey(rnd);
			var iparams = key.ComputeCoordinatorParameters();
			Assert.Equal(Generators.GV, iparams.I);

			// Coordinator key is (1, 1, 1, 1, 1)
			rnd = new MockRandom();
			rnd.GetScalarResults.AddRange(Enumerable.Repeat(Scalar.One, 5));
			key = new CoordinatorSecretKey(rnd);
			iparams = key.ComputeCoordinatorParameters();
			Assert.Equal(Generators.Gw + Generators.Gwp, iparams.Cw);
			Assert.Equal(Generators.GV - Generators.Gx0 - Generators.Gx1 - Generators.Ga, iparams.I);
		}
	}
}