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
		public static ZkExponentProof CreateProof(Scalar exponent, GroupElement publicPoint, GroupElement generator, WasabiRandom? random = null)
		{
			Guard.False($"{nameof(exponent)}.{nameof(exponent.IsOverflow)}", exponent.IsOverflow);
			Guard.False($"{nameof(exponent)}.{nameof(exponent.IsZero)}", exponent.IsZero);
			Guard.False($"{nameof(generator)}.{nameof(generator.IsInfinity)}", generator.IsInfinity);
			Guard.False($"{nameof(publicPoint)}.{nameof(publicPoint.IsInfinity)}", publicPoint.IsInfinity);
			if (publicPoint != exponent * generator)
			{
				throw new InvalidOperationException($"{nameof(publicPoint)} != {nameof(exponent)} * {nameof(generator)}");
			}

			var randomScalar = GetNonZeroRandomScalar(random);
			var randomPoint = randomScalar * generator;
			var challenge = ZkChallenge.Build(publicPoint, randomPoint);

			var response = randomScalar + exponent * challenge;

			return new ZkExponentProof(randomPoint, response);
		}

		private static Scalar GetNonZeroRandomScalar(WasabiRandom? random = null)
		{
			var disposeRandom = false;
			if (random is null)
			{
				disposeRandom = true;
				random = new SecureRandom();
			}

			try
			{
				Scalar randomScalar = Scalar.Zero;
				var gotZero = false;

				while (randomScalar == Scalar.Zero)
				{
					randomScalar = random.GetScalar();

					// We can tolerate zero scalar only once.
					// Its probability is null to get it even once, but getting it twice is a catastrophe.
					if (randomScalar == Scalar.Zero)
					{
						if (gotZero)
						{
							throw new InvalidOperationException("Something is wrong with the random generation. It should not return zero scalar twice in a row.");
						}
						else
						{
							gotZero = true;
						}
					}
				}

				if (randomScalar.IsOverflow)
				{
					throw new InvalidOperationException("Something is wrong with the random generation. It should not return overflown scalar.");
				}
				return randomScalar;
			}
			finally
			{
				if (disposeRandom)
				{
					(random as IDisposable)?.Dispose();
				}
			}
		}
	}
}
