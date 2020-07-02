using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.WabiSabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class KnowledgeOfExponentTests
	{
		[Fact]
		public void VerifyProof()
		{
			var exponent = new Scalar(5);
			var proof = ZkProver.CreateProof(exponent);

			Assert.True(ZkVerifier.Verify(proof));
		}
	}
}
