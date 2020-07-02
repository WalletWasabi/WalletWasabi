using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.WabiSabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class ZkExponentTests
	{
		[Fact]
		public void VerifyBasicProof()
		{
			var exponent = new Scalar(5);
			var proof = ZkProver.CreateProof(exponent);
			Assert.True(ZkVerifier.Verify(proof));
		}

		[Theory]
		[InlineData(1)]
		[InlineData(3)]
		[InlineData(uint.MaxValue)]
		public void VerifySimpleProof(uint scalarSeed)
		{
			var exponent = new Scalar(scalarSeed);
			var proof = ZkProver.CreateProof(exponent);
			Assert.True(ZkVerifier.Verify(proof));
		}

		[Fact]
		public void ScalarCannotBeZero()
		{
			var exponent = new Scalar(0);
			Assert.Throws<ArgumentOutOfRangeException>(() => ZkProver.CreateProof(exponent));

			exponent = Scalar.Zero;
			Assert.Throws<ArgumentOutOfRangeException>(() => ZkProver.CreateProof(exponent));
		}

		[Theory]
		[InlineData(int.MaxValue)]
		[InlineData(uint.MaxValue)]
		public void VerifyLargeScalar(uint val)
		{
			// var exponent = new Scalar(val, val, val, val, val - 1, val, val, val);
			var exponent = new Scalar(val, val, val, val, val, val, val, val);
			var proof = ZkProver.CreateProof(exponent);
			Assert.True(ZkVerifier.Verify(proof));
		}

		[Fact]
		public void BuildChallenge()
		{
			var point1 = (EC.G * new Scalar(3)).ToGroupElement();
			var point2 = (EC.G * new Scalar(7)).ToGroupElement();

			var publicPoint = point1;
			var randomPoint = EC.G;
			var challenge = ZkChallenge.Build(publicPoint, randomPoint);
			Assert.Equal("secp256k1_scalar  = { 0x18F5F6E4UL, 0x5B8787FEUL, 0x14727A9EUL, 0xD7F74A19UL, 0xDA006442UL, 0x79492D72UL, 0xD551E4A3UL, 0xAC29D7FCUL }", challenge.ToC(""));

			publicPoint = EC.G;
			randomPoint = point2;
			challenge = ZkChallenge.Build(publicPoint, randomPoint);
			Assert.Equal("secp256k1_scalar  = { 0xCC5AEF16UL, 0x09E5B262UL, 0xD750EAC5UL, 0xE14DFBF8UL, 0x98819DF3UL, 0xB532F187UL, 0x4897CB03UL, 0x2179CD2FUL }", challenge.ToC(""));

			publicPoint = point1;
			randomPoint = point2;
			challenge = ZkChallenge.Build(publicPoint, randomPoint);
			Assert.Equal("secp256k1_scalar  = { 0x79342242UL, 0x9F7E0258UL, 0x1E6DAA75UL, 0xD374DA42UL, 0x3AB38E04UL, 0x30837227UL, 0x35A22847UL, 0x887A4A08UL }", challenge.ToC(""));
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

			publicPoint = point1;
			randomPoint = point1;
			Assert.Throws<InvalidOperationException>(() => ZkChallenge.Build(publicPoint, randomPoint));
		}

		[Fact]
		public void InvalidProof()
		{
			var point1 = (EC.G * new Scalar(3)).ToGroupElement();
			var point2 = (EC.G * new Scalar(7)).ToGroupElement();
			var scalar = new Scalar(11);
			var invalidPoint = new GE(new FE(1), new FE(3));

			var proof = new ZkExponentProof(point1, point2, scalar);
			Assert.False(ZkVerifier.Verify(proof));

			Assert.Throws<ArgumentOutOfRangeException>(() => new ZkExponentProof(invalidPoint, point2, scalar));
			Assert.Throws<ArgumentOutOfRangeException>(() => new ZkExponentProof(point1, invalidPoint, scalar));

			Assert.Throws<ArgumentOutOfRangeException>(() => new ZkExponentProof(GEJ.Infinity.ToGroupElement(), point2, scalar));
			Assert.Throws<ArgumentOutOfRangeException>(() => new ZkExponentProof(point1, GEJ.Infinity.ToGroupElement(), scalar));

			Assert.Throws<InvalidOperationException>(() => new ZkExponentProof(point1, point1, scalar));
		}
	}
}
