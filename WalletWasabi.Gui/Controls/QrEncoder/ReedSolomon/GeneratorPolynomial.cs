using System;
using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding.ReedSolomon
{
	/// <summary>
	/// Description of GeneratorPolynomial.
	/// </summary>
	internal sealed class GeneratorPolynomial
	{
		private readonly GaloisField256 m_gfield;
		
		private List<Polynomial> m_cacheGenerator;
		
		/// <summary>
		/// After create GeneratorPolynomial. Keep it as long as possible. 
		/// Unless QRCode encode is done or no more QRCode need to generate.
		/// </summary>
		internal GeneratorPolynomial(GaloisField256 gfield)
		{
			m_gfield = gfield;
			m_cacheGenerator = new List<Polynomial>(10);
			m_cacheGenerator.Add(new Polynomial(m_gfield, new int[]{1}));
		}
		
		/// <summary>
		/// Get generator by degree. (Largest degree for that generator)
		/// </summary>
		/// <returns>Generator</returns>
		internal Polynomial GetGenerator(int degree)
		{
			if(degree >= m_cacheGenerator.Count)
				BuildGenerator(degree);
			return m_cacheGenerator[degree];
		}
		
		/// <summary>
		/// Build Generator if we can not find specific degree of generator from cache
		/// </summary>
		private void BuildGenerator(int degree)
		{
			lock(m_cacheGenerator)
			{
				int currentCacheLength = m_cacheGenerator.Count;
				if(degree >= currentCacheLength)
				{
					Polynomial lastGenerator = m_cacheGenerator[currentCacheLength - 1];
				
					for(int d = currentCacheLength; d <= degree; d++)
					{
						Polynomial nextGenerator = lastGenerator.Multiply(new Polynomial(m_gfield, new int[]{1, m_gfield.Exponent(d - 1)}));
						m_cacheGenerator.Add(nextGenerator);
						lastGenerator = nextGenerator;
					}
				}
			}
		}
		
	}
}
