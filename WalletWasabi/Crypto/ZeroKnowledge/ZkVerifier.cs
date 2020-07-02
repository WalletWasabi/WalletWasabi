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
		public static bool Verify(ZkProof proof)
		{
			Guard.NotNull(nameof(proof), proof);

			var publicPoint = proof.PublicPoint;
			var randomPoint = proof.RandomPoint;
			var challenge = ZkChallenge.Build(publicPoint, randomPoint);

			var left = (publicPoint * challenge + randomPoint).ToGroupElement();
			var right = (EC.G * proof.Response).ToGroupElement();
			return (left.IsInfinity && right.IsInfinity) || (left.x == right.x && left.y == right.y);
		}
	}
}
