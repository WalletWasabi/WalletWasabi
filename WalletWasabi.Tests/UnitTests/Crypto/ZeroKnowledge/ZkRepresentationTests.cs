using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.ZeroKnowledge;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.ZeroKnowledge
{
	public class ZkRepresentationTests
	{
		[Theory]
		[InlineData(1, 1)]
		[InlineData(1, 2)]
		[InlineData(3, 5)]
		[InlineData(5, 7)]
		[InlineData(7, 11)]
		[InlineData(short.MaxValue, uint.MaxValue)]
		[InlineData(int.MaxValue, uint.MaxValue)]
		[InlineData(uint.MaxValue, uint.MaxValue)]
		public void End2EndVerifiesSimpleProof(uint scalarSeed1, uint scalarSeed2)
		{
			var exponents = new[] { new Scalar(scalarSeed1), new Scalar(scalarSeed2) };
			var generators = new[] { GroupElement.G, GroupElement.Ga };
			var publicPoint = GroupElement.Infinity;
			for (int i = 0; i < exponents.Length; i++)
			{
				publicPoint += exponents[i] * generators[i];
			}
			var proof = ZkProver.CreateProof(exponents, publicPoint, generators);
			Assert.True(ZkVerifier.Verify(proof, publicPoint, generators));
		}
	}
}
