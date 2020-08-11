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
		public static ZkExponentProof CreateProof(Scalar exponent, GroupElement publicPoint, GroupElement generator)
		{
			Guard.False($"{nameof(exponent)}.{nameof(exponent.IsOverflow)}", exponent.IsOverflow);
			Guard.False($"{nameof(exponent)}.{nameof(exponent.IsZero)}", exponent.IsZero);
			Guard.False($"{nameof(generator)}.{nameof(generator.IsInfinity)}", generator.IsInfinity);
			Guard.False($"{nameof(publicPoint)}.{nameof(publicPoint.IsInfinity)}", publicPoint.IsInfinity);
			if (publicPoint != exponent * generator)
			{
				throw new InvalidOperationException($"{nameof(publicPoint)} != {nameof(exponent)} * {nameof(generator)}");
			}

			using var secureRandom = new SecureRandom();
			Scalar randomScalar = Scalar.Zero;
			while (randomScalar == Scalar.Zero)
			{
				randomScalar = secureRandom.GetScalar();
			}
			var randomPoint = randomScalar * generator;
			var challenge = ZkChallenge.Build(publicPoint, randomPoint);

			var response = randomScalar + exponent * challenge;

			return new ZkExponentProof(randomPoint, response);
		}
	}
}
