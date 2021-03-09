using Gma.QrCodeNet.Encoding.EncodingRegion;
using System;

namespace Gma.QrCodeNet.Encoding.Masking
{
	public static class MatrixExtensions
	{
		public static TriStateMatrix Xor(this TriStateMatrix first, Pattern second, ErrorCorrectionLevel errorLevel)
		{
			TriStateMatrix result = XorMatrix(first, second);
			result.EmbedFormatInformation(errorLevel, second);
			return result;
		}

		private static TriStateMatrix XorMatrix(TriStateMatrix first, BitMatrix second)
		{
			int width = first.Width;
			TriStateMatrix maskedMatrix = new(width);
			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < width; y++)
				{
					MatrixStatus states = first.MStatus(x, y);
					switch (states)
					{
						case MatrixStatus.NoMask:
							maskedMatrix[x, y, MatrixStatus.NoMask] = first[x, y];
							break;

						case MatrixStatus.Data:
							maskedMatrix[x, y, MatrixStatus.Data] = first[x, y] ^ second[x, y];
							break;

						default:
							throw new ArgumentException($"{nameof(TriStateMatrix)} has None value cell.", nameof(first));
					}
				}
			}

			return maskedMatrix;
		}

		public static TriStateMatrix Apply(this TriStateMatrix matrix, Pattern pattern, ErrorCorrectionLevel errorLevel) => matrix.Xor(pattern, errorLevel);
	}
}
