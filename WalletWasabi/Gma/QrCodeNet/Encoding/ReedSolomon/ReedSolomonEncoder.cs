using System;
using WalletWasabi.Helpers;

namespace Gma.QrCodeNet.Encoding.ReedSolomon
{
	internal sealed class ReedSolomonEncoder
	{
		/// <summary>
		/// Encode an array of data codeword with GaloisField 256.
		/// </summary>
		/// <param name="dataBytes">Array of data codewords for a single block.</param>
		/// <param name="numECBytes">Number of error correction codewords for data codewords</param>
		/// <param name="generatorPoly">Cached or newly create GeneratorPolynomial</param>
		/// <returns>Return error correction codewords array</returns>
		internal static byte[] Encode(byte[] dataBytes, int numECBytes, GeneratorPolynomial generatorPoly)
		{
			int dataLength = dataBytes.Length;
			Guard.NotNull(nameof(generatorPoly), generatorPoly);

			if (dataLength == 0)
			{
				throw new ArgumentException("There is no data bytes to encode.");
			}

			if (numECBytes <= 0)
			{
				throw new ArgumentException("No Error Correction bytes.");
			}

			int[] toEncode = ConvertToIntArray(dataBytes, dataLength, numECBytes);

			Polynomial generator = generatorPoly.GetGenerator(numECBytes);

			Polynomial dataPoly = new(generator.GField, toEncode);

			PolyDivideStruct divideResult = dataPoly.Divide(generator);

			int[] remainderCoeffs = divideResult.Remainder.Coefficients;

			return ConvertTosByteArray(remainderCoeffs, numECBytes);
		}

		/// <summary>
		/// Convert data codewords to int array. And add error correction space at end of that array
		/// </summary>
		/// <param name="dataBytes">Data codewords array</param>
		/// <param name="dataLength">Data codewords length</param>
		/// <param name="numECBytes">Num of error correction bytes</param>
		/// <returns>Int array for data codewords array follow by error correction space</returns>
		private static int[] ConvertToIntArray(byte[] dataBytes, int dataLength, int numECBytes)
		{
			int[] resultArray = new int[dataLength + numECBytes];

			for (int index = 0; index < dataLength; index++)
			{
				resultArray[index] = dataBytes[index] & 0xff;
			}

			return resultArray;
		}

		/// <summary>
		/// Reassembly error correction codewords. As Polynomial class will eliminate zero monomial at front.
		/// </summary>
		/// <param name="remainder">Remainder byte array after divide. </param>
		/// <param name="numECBytes">Error correction codewords length</param>
		/// <returns>Error correction codewords</returns>
		private static byte[] ConvertTosByteArray(int[] remainder, int numECBytes)
		{
			int remainderLength = remainder.Length;
			if (remainderLength > numECBytes)
			{
				throw new ArgumentException($"Num of {nameof(remainder)} bytes cannot be larger than {nameof(numECBytes)}.");
			}

			int numZeroCoeffs = numECBytes - remainderLength;

			byte[] resultArray = new byte[numECBytes];
			for (int index = 0; index < numZeroCoeffs; index++)
			{
				resultArray[index] = 0;
			}

			for (int rIndex = 0; rIndex < remainderLength; rIndex++)
			{
				resultArray[numZeroCoeffs + rIndex] = (byte)remainder[rIndex];
			}

			return resultArray;
		}
	}
}
