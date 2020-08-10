using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class ZkProver
	{
		public static ZkExponentProof CreateProof(Scalar exponent)
		{
			Guard.False($"{nameof(exponent)}.{nameof(exponent.IsOverflow)}", exponent.IsOverflow);
			Guard.False($"{nameof(exponent)}.{nameof(exponent.IsZero)}", exponent.IsZero);

			var publicPoint = exponent * GroupElement.G;
			using var secureRandom = new SecureRandom();
			Scalar randomScalar = Scalar.Zero;
			while (randomScalar == Scalar.Zero)
			{
				randomScalar = secureRandom.GetScalar();
			}
			var randomPoint = randomScalar * GroupElement.G;
			var challenge = ZkChallenge.Build(publicPoint, randomPoint);

			var response = randomScalar + exponent * challenge;

			return new ZkExponentProof(publicPoint, randomPoint, response);
		}
	}
}
