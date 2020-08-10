using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class ZkChallenge
	{
		public static Scalar Build(GroupElement publicPoint, GroupElement randomPoint)
		{
			Guard.False($"{nameof(publicPoint)}.{nameof(publicPoint.IsInfinity)}", publicPoint.IsInfinity);
			Guard.False($"{nameof(randomPoint)}.{nameof(randomPoint.IsInfinity)}", randomPoint.IsInfinity);

			if (publicPoint == randomPoint)
			{
				throw new InvalidOperationException($"{nameof(publicPoint)} and {nameof(randomPoint)} should not be equal.");
			}

			var concatenation = ByteHelpers.Combine(
				publicPoint.ToBytes(),
				randomPoint.ToBytes());
			using var sha256 = System.Security.Cryptography.SHA256.Create();
			var hash = sha256.ComputeHash(concatenation);
			var challenge = new Scalar(hash);
			return challenge;
		}
	}
}
