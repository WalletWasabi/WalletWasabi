using NBitcoin.Secp256k1;
using System;
using System.Linq;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class CredentialIssuerKeyTests
	{
		[Fact]
		public void GenerateCredentialIssuerParameters()
		{
			// Coordinator key is (0, 0, 0, 0, 0)
			var rnd = new MockRandom();
			rnd.GetScalarResults.AddRange(Enumerable.Repeat(Scalar.Zero, 5));
			var key = new CredentialIssuerSecretKey(rnd);
			var ex = Assert.Throws<ArgumentException>(key.ComputeCredentialIssuerParameters);
			Assert.StartsWith("Point at infinity is not a valid value.", ex.Message);

			// Coordinator key is (0, 0, 1, 1, 1)
			rnd = new MockRandom();
			rnd.GetScalarResults.AddRange(Enumerable.Repeat(Scalar.Zero, 2));
			rnd.GetScalarResults.AddRange(Enumerable.Repeat(Scalar.One, 3));
			key = new CredentialIssuerSecretKey(rnd);
			ex = Assert.Throws<ArgumentException>(key.ComputeCredentialIssuerParameters);
			Assert.StartsWith("Point at infinity is not a valid value.", ex.Message);

			// Coordinator key is (1, 1, 0, 0, 0)
			rnd = new MockRandom();
			rnd.GetScalarResults.AddRange(Enumerable.Repeat(Scalar.One, 2));
			rnd.GetScalarResults.AddRange(Enumerable.Repeat(Scalar.Zero, 3));
			key = new CredentialIssuerSecretKey(rnd);
			var iparams = key.ComputeCredentialIssuerParameters();
			Assert.Equal(Generators.GV, iparams.I);

			// Coordinator key is (1, 1, 1, 1, 1)
			rnd = new MockRandom();
			rnd.GetScalarResults.AddRange(Enumerable.Repeat(Scalar.One, 5));
			key = new CredentialIssuerSecretKey(rnd);
			iparams = key.ComputeCredentialIssuerParameters();
			Assert.Equal(Generators.Gw + Generators.Gwp, iparams.Cw);
			Assert.Equal(Generators.GV - Generators.Gx0 - Generators.Gx1 - Generators.Ga, iparams.I);
		}
	}
}
