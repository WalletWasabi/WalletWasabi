using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class ZkVerifier
	{
		public static bool Verify(ZkExponentProof proof)
		{
			Guard.NotNull(nameof(proof), proof);

			var publicPoint = proof.PublicPoint;
			var randomPoint = proof.RandomPoint;
			var challenge = ZkChallenge.Build(publicPoint, randomPoint);

			var a = (publicPoint * challenge + randomPoint).ToGroupElement();
			var b = (EC.G * proof.Response).ToGroupElement();
			return Secp256k1Helpers.Equals(a, b);
		}
	}
}
