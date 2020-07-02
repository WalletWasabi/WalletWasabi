using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class ZkChallenge
	{
		public static Scalar Build(GE publicPoint, GE randomPoint)
		{
			var concatenation = ByteHelpers.Combine(
				publicPoint.x.ToBytes(),
				publicPoint.y.ToBytes(),
				randomPoint.x.ToBytes(),
				randomPoint.y.ToBytes());
			using var sha256 = System.Security.Cryptography.SHA256.Create();
			var hash = sha256.ComputeHash(concatenation);
			var challenge = new Scalar(hash);
			return challenge;
		}
	}
}
