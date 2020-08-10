using System;
using System.Collections.Generic;
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

			var a = challenge * publicPoint + randomPoint;
			var b = proof.Response * GroupElement.G;
			return a == b;
		}
	}
}
