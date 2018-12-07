using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding.ReedSolomon
{
	/// <summary>
	/// Description of GeneratorPolynomial.
	/// </summary>
	internal sealed class GeneratorPolynomial
	{
		private readonly GaloisField256 M_gfield;

		private List<Polynomial> _m_cacheGenerator;

		/// <summary>
		/// After create GeneratorPolynomial. Keep it as long as possible.
		/// Unless QRCode encode is done or no more QRCode need to generate.
		/// </summary>
		internal GeneratorPolynomial(GaloisField256 gfield)
		{
			M_gfield = gfield;
			_m_cacheGenerator = new List<Polynomial>(10)
			{
				new Polynomial(M_gfield, new int[] { 1 })
			};
		}

		/// <summary>
		/// Get generator by degree. (Largest degree for that generator)
		/// </summary>
		/// <returns>Generator</returns>
		internal Polynomial GetGenerator(int degree)
		{
			if (degree >= _m_cacheGenerator.Count)
				BuildGenerator(degree);
			return _m_cacheGenerator[degree];
		}

		/// <summary>
		/// Build Generator if we can not find specific degree of generator from cache
		/// </summary>
		private void BuildGenerator(int degree)
		{
			lock (_m_cacheGenerator)
			{
				int currentCacheLength = _m_cacheGenerator.Count;
				if (degree >= currentCacheLength)
				{
					Polynomial lastGenerator = _m_cacheGenerator[currentCacheLength - 1];

					for (int d = currentCacheLength; d <= degree; d++)
					{
						Polynomial nextGenerator = lastGenerator.Multiply(new Polynomial(M_gfield, new int[] { 1, M_gfield.Exponent(d - 1) }));
						_m_cacheGenerator.Add(nextGenerator);
						lastGenerator = nextGenerator;
					}
				}
			}
		}
	}
}
