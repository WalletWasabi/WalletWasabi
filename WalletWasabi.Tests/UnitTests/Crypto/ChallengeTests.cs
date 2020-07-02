using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.ZeroKnowledge;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class ChallengeTests
	{
		[Fact]
		public void BuildChallenge()
		{
			var point1 = (EC.G * new Scalar(1)).ToGroupElement();
			var point2 = (EC.G * new Scalar(2)).ToGroupElement();

			var publicPoint = EC.G;
			var randomPoint = EC.G;
			var challenge = ZkChallenge.Build(publicPoint, randomPoint);
			Assert.Equal("secp256k1_scalar  = { 0x75BF65A1UL, 0x0F13F9BDUL, 0x97C721AEUL, 0x616E9700UL, 0xBF4E7CA7UL, 0x283A3041UL, 0x4767F567UL, 0xFD0A9D2AUL }", challenge.ToC(""));

			publicPoint = point1;
			randomPoint = EC.G;
			challenge = ZkChallenge.Build(publicPoint, randomPoint);
			Assert.Equal("secp256k1_scalar  = { 0x75BF65A1UL, 0x0F13F9BDUL, 0x97C721AEUL, 0x616E9700UL, 0xBF4E7CA7UL, 0x283A3041UL, 0x4767F567UL, 0xFD0A9D2AUL }", challenge.ToC(""));

			publicPoint = EC.G;
			randomPoint = point2;
			challenge = ZkChallenge.Build(publicPoint, randomPoint);
			Assert.Equal("secp256k1_scalar  = { 0x1B37E3AEUL, 0x019AC818UL, 0x0375B82CUL, 0x1DABC711UL, 0x0D056003UL, 0x1DB920CAUL, 0x3536D66BUL, 0x3824CDF4UL }", challenge.ToC(""));

			publicPoint = point1;
			randomPoint = point2;
			challenge = ZkChallenge.Build(publicPoint, randomPoint);
			Assert.Equal("secp256k1_scalar  = { 0x1B37E3AEUL, 0x019AC818UL, 0x0375B82CUL, 0x1DABC711UL, 0x0D056003UL, 0x1DB920CAUL, 0x3536D66BUL, 0x3824CDF4UL }", challenge.ToC(""));
		}

		[Fact]
		public void InvalidChallenge()
		{
			var point1 = (EC.G * new Scalar(1)).ToGroupElement();
			var point2 = (EC.G * new Scalar(2)).ToGroupElement();
			ZkChallenge.Build(point1, point2); // Make sure the points are valid.

			var publicPoint = GE.Infinity;
			var randomPoint = GE.Infinity;
			Assert.Throws<ArgumentOutOfRangeException>(() => ZkChallenge.Build(publicPoint, randomPoint));

			publicPoint = point1;
			randomPoint = GE.Infinity;
			Assert.Throws<ArgumentOutOfRangeException>(() => ZkChallenge.Build(publicPoint, randomPoint));

			publicPoint = GE.Infinity;
			randomPoint = point2;
			Assert.Throws<ArgumentOutOfRangeException>(() => ZkChallenge.Build(publicPoint, randomPoint));

			publicPoint = new GE(FE.Zero, FE.Zero);
			randomPoint = point2;
			Assert.Throws<ArgumentOutOfRangeException>(() => ZkChallenge.Build(publicPoint, randomPoint));

			publicPoint = new GE(new FE(1), FE.Zero);
			randomPoint = point2;
			Assert.Throws<ArgumentOutOfRangeException>(() => ZkChallenge.Build(publicPoint, randomPoint));

			publicPoint = new GE(new FE(1), new FE(3));
			randomPoint = point2;
			Assert.Throws<ArgumentOutOfRangeException>(() => ZkChallenge.Build(publicPoint, randomPoint));

			publicPoint = point1;
			randomPoint = new GE(new FE(1), new FE(3));
			Assert.Throws<ArgumentOutOfRangeException>(() => ZkChallenge.Build(publicPoint, randomPoint));

			publicPoint = point1;
			randomPoint = new GE(new FE(1), new FE(3));
			Assert.Throws<ArgumentOutOfRangeException>(() => ZkChallenge.Build(publicPoint, randomPoint));

			publicPoint = new GE(new FE(1), new FE(3));
			randomPoint = new GE(new FE(7), new FE(11));
			Assert.Throws<ArgumentOutOfRangeException>(() => ZkChallenge.Build(publicPoint, randomPoint));
		}
	}
}
