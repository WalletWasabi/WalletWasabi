using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.ZeroKnowledge;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.ZeroKnowledge
{
	public class KnowledgeOfRepresentationTests
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
		public void End2EndVerificationSimple(uint scalarSeed1, uint scalarSeed2)
		{
			var secrets = new[] { new Scalar(scalarSeed1), new Scalar(scalarSeed2) };
			var generators = new[] { Generators.G, Generators.Ga };
			var publicPoint = GroupElement.Infinity;
			for (int i = 0; i < secrets.Length; i++)
			{
				publicPoint += secrets[i] * generators[i];
			}
			var proof = Prover.CreateProof(secrets, publicPoint, generators);
			Assert.True(Verifier.Verify(proof, publicPoint, generators));
		}
	}
}
