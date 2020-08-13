using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class ZkProver
	{
		public static ZkKnowledgeOfExponent CreateProof(Scalar exponent, GroupElement publicPoint, GroupElement generator, WasabiRandom? random = null)
		{
			Guard.False($"{nameof(exponent)}.{nameof(exponent.IsOverflow)}", exponent.IsOverflow);
			Guard.False($"{nameof(exponent)}.{nameof(exponent.IsZero)}", exponent.IsZero);
			Guard.False($"{nameof(generator)}.{nameof(generator.IsInfinity)}", generator.IsInfinity);
			Guard.False($"{nameof(publicPoint)}.{nameof(publicPoint.IsInfinity)}", publicPoint.IsInfinity);
			if (publicPoint != exponent * generator)
			{
				throw new InvalidOperationException($"{nameof(publicPoint)} != {nameof(exponent)} * {nameof(generator)}");
			}

			var proof = CreateProof(new[] { exponent }, publicPoint, new[] { generator }, random);

			return new ZkKnowledgeOfExponent(proof.Nonce, proof.Responses.First());
		}

		public static ZkKnowledgeOfRepresentation CreateProof(IEnumerable<Scalar> exponents, GroupElement publicPoint, IEnumerable<GroupElement> generators, WasabiRandom? random = null)
		{
			Guard.False($"{nameof(publicPoint)}.{nameof(publicPoint.IsInfinity)}", publicPoint.IsInfinity);

			var exponentArray = exponents.ToArray();
			var generatorArray = generators.ToArray();

			if (exponentArray.Length != generatorArray.Length)
			{
				throw new ArgumentException($"Same number of exponents and generators must be provided. Exponents: {exponentArray.Length}, Generators: {generatorArray.Length}");
			}

			var nonce = GroupElement.Infinity;
			var randomScalars = new List<Scalar>();
			for (int i = 0; i < exponentArray.Length; i++)
			{
				var exponent = exponentArray[i];
				var generator = generatorArray[i];

				Guard.False($"{nameof(exponent)}.{nameof(exponent.IsOverflow)}", exponent.IsOverflow);
				Guard.False($"{nameof(exponent)}.{nameof(exponent.IsZero)}", exponent.IsZero);
				Guard.False($"{nameof(generator)}.{nameof(generator.IsInfinity)}", generator.IsInfinity);

				var randomScalar = GetNonZeroRandomScalar(random);
				randomScalars.Add(randomScalar);
				var randomPoint = randomScalar * generator;

				nonce += randomPoint;
			}

			var challenge = ZkChallenge.HashToScalar(new[] { publicPoint, nonce }.Concat(generators).ToArray());

			var responses = new List<Scalar>();
			for (int i = 0; i < exponentArray.Length; i++)
			{
				var exponent = exponentArray[i];
				var randomScalar = randomScalars.ToArray()[i];
				var response = randomScalar + exponent * challenge;
				responses.Add(response);
			}

			return new ZkKnowledgeOfRepresentation(nonce, responses);
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
