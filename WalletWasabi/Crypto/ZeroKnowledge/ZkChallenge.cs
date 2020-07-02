using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class ZkChallenge
	{
		public static Scalar Build(GE publicPoint, GE randomPoint)
		{
			Guard.True($"{nameof(publicPoint)}.{nameof(publicPoint.IsValidVariable)}", publicPoint.IsValidVariable);
			Guard.True($"{nameof(randomPoint)}.{nameof(randomPoint.IsValidVariable)}", randomPoint.IsValidVariable);
			if (Secp256k1Helpers.Equals(publicPoint, randomPoint))
			{
				throw new InvalidOperationException($"{nameof(publicPoint)} and {nameof(randomPoint)} should not be equal.");
			}

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
