using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding.ReedSolomon;

/// <summary>
/// Description of GeneratorPolynomial.
/// </summary>
internal sealed class GeneratorPolynomial
{
	/// <summary>
	/// After create GeneratorPolynomial. Keep it as long as possible.
	/// Unless QRCode encode is done or no more QRCode need to generate.
	/// </summary>
	internal GeneratorPolynomial(GaloisField256 gfield)
	{
		Gfield = gfield;
		CacheGenerator = new List<Polynomial>(10)
			{
				new Polynomial(Gfield, new int[] { 1 })
			};
	}

	private GaloisField256 Gfield { get; }

	private List<Polynomial> CacheGenerator { get; }

	/// <summary>
	/// Get generator by degree. (Largest degree for that generator)
	/// </summary>
	/// <returns>Generator</returns>
	internal Polynomial GetGenerator(int degree)
	{
		if (degree >= CacheGenerator.Count)
		{
			BuildGenerator(degree);
		}

		return CacheGenerator[degree];
	}

	/// <summary>
	/// Build Generator if we cannot find specific degree of generator from cache
	/// </summary>
	private void BuildGenerator(int degree)
	{
		lock (CacheGenerator)
		{
			int currentCacheLength = CacheGenerator.Count;
			if (degree >= currentCacheLength)
			{
				Polynomial lastGenerator = CacheGenerator[currentCacheLength - 1];

				for (int d = currentCacheLength; d <= degree; d++)
				{
					Polynomial nextGenerator = lastGenerator.Multiply(new Polynomial(Gfield, new int[] { 1, Gfield.Exponent(d - 1) }));
					CacheGenerator.Add(nextGenerator);
					lastGenerator = nextGenerator;
				}
			}
		}
	}
}
