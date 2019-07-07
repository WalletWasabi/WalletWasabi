using System;

namespace Gma.QrCodeNet.Encoding.Masking.Scoring
{
	/// <summary>
	/// ISO/IEC 18004:2000 Chapter 8.8.2 Page 52
	/// </summary>
	internal class Penalty4 : Penalty
	{
		/// <summary>
		/// Calculate penalty value for Fourth rule.
		/// Perform O(n) search for available x modules
		/// </summary>
		internal override int PenaltyCalculate(BitMatrix matrix)
		{
			int width = matrix.Width;
			int DarkBitCount = 0;

			for (int j = 0; j < width; j++)
			{
				for (int i = 0; i < width; i++)
				{
					if (matrix[i, j])
					{
						DarkBitCount++;
					}
				}
			}

			int MatrixCount = width * width;

			double ratio = (double)DarkBitCount / MatrixCount;

			return Math.Abs((int)((ratio * 100) - 50)) / 5 * 10;
		}
	}
}
