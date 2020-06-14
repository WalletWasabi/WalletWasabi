using System;

namespace Gma.QrCodeNet.Encoding.EncodingRegion
{
	/// <remarks>ISO/IEC 18004:2000 Chapter 8.7.3 Page 46</remarks>
	internal static class Codeword
	{
		internal static void TryEmbedCodewords(this TriStateMatrix tsMatrix, BitList codewords)
		{
			int sWidth = tsMatrix.Width;
			int codewordsSize = codewords.Count;

			int bitIndex = 0;
			int directionUp = -1;

			int x = sWidth - 1;
			int y = sWidth - 1;

			while (x > 0)
			{
				// Skip vertical timing pattern
				if (x == 6)
				{
					x -= 1;
				}

				while (y >= 0 && y < sWidth)
				{
					for (int xOffset = 0; xOffset < 2; xOffset++)
					{
						int xPos = x - xOffset;
						if (tsMatrix.MStatus(xPos, y) == MatrixStatus.None)
						{
							bool bit;
							if (bitIndex < codewordsSize)
							{
								bit = codewords[bitIndex];
								bitIndex++;
							}
							else
							{
								bit = false;
							}

							tsMatrix[xPos, y, MatrixStatus.Data] = bit;
						}
					}

					y = NextY(y, directionUp);
				}

				directionUp = ChangeDirection(directionUp);
				y = NextY(y, directionUp);
				x -= 2;
			}

			if (bitIndex != codewordsSize)
			{
				throw new Exception($"Not all bits from {nameof(codewords)} consumed by matrix: {bitIndex} / {codewordsSize}.");
			}
		}

		internal static int NextY(int y, int directionUp)
		{
			return y + directionUp;
		}

		internal static int ChangeDirection(int directionUp)
		{
			return -directionUp;
		}
	}
}
