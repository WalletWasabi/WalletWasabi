using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class ZkVerifier
	{
		public static bool Verify(ZkExponentProof proof, GroupElement publicPoint, GroupElement generator)
		{
			Guard.False($"{nameof(generator)}.{nameof(generator.IsInfinity)}", generator.IsInfinity);
			Guard.False($"{nameof(publicPoint)}.{nameof(publicPoint.IsInfinity)}", publicPoint.IsInfinity);

			var randomPoint = proof.RandomPoint;
			var challenge = ZkChallenge.Build(publicPoint, randomPoint);

			var a = challenge * publicPoint + randomPoint;
			var b = proof.Response * generator;
			return a == b;
		}
	}
}
