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
	public static class Prover
	{
		public static KnowledgeOfExponent CreateProof(Scalar exponent, GroupElement publicPoint, GroupElement generator, WasabiRandom? random = null)
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

			return new KnowledgeOfExponent(proof.Nonce, proof.Responses.First());
		}

		public static KnowledgeOfRepresentation CreateProof(IEnumerable<Scalar> exponents, GroupElement publicPoint, IEnumerable<GroupElement> generators, WasabiRandom? random = null)
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

			var challenge = Challenge.HashToScalar(new[] { publicPoint, nonce }.Concat(generators).ToArray());

			var responses = new List<Scalar>();
			for (int i = 0; i < exponentArray.Length; i++)
			{
				var exponent = exponentArray[i];
				var randomScalar = randomScalars.ToArray()[i];
				var response = randomScalar + exponent * challenge;
				responses.Add(response);
			}

			return new KnowledgeOfRepresentation(nonce, responses);
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
				var scalar = random.GetScalar(allowZero: false);
				// Sanity checks:

				if (scalar.IsOverflow || scalar.IsZero)
				{
					throw new InvalidOperationException("Bloody murder! Random generator served invalid scalar.");
				}

				return scalar;
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
