namespace Gma.QrCodeNet.Encoding.Masking.Scoring;

/// <summary>
/// ISO/IEC 18004:2000 Chapter 8.8.2 Page 52
/// </summary>
internal class Penalty1 : Penalty
{
	/// <summary>
	/// Calculate penalty value for first rule.
	/// </summary>
	internal override int PenaltyCalculate(BitMatrix matrix)
	{
		int penaltyValue = PenaltyCalculation(matrix, true) + PenaltyCalculation(matrix, false);
		return penaltyValue;
	}

	private int PenaltyCalculation(BitMatrix matrix, bool isHorizontal)
	{
		int penalty = 0;
		int width = matrix.Width;

		int i = 0;
		int j = 0;

		while (i < width)
		{
			while (j < width - 4)
			{
				bool preBit = isHorizontal
					? matrix[j + 4, i]
					: matrix[i, j + 4];
				int numSameBitCell = 1;

				for (int x = 1; x <= 4; x++)
				{
					bool bit = isHorizontal
						? matrix[j + 4 - x, i]
						: matrix[i, j + 4 - x];
					if (bit == preBit)
					{
						numSameBitCell++;
					}
					else
					{
						break;
					}
				}

				if (numSameBitCell == 1)
				{
					j += 4;
				}
				else
				{
					int x = 5;
					while ((j + x) < width)
					{
						bool bit = isHorizontal
							? matrix[j + x, i]
							: matrix[i, j + x];
						if (bit == preBit)
						{
							numSameBitCell++;
						}
						else
						{
							break;
						}
						x++;
					}
					if (numSameBitCell >= 5)
					{
						penalty += (3 + (numSameBitCell - 5));
					}

					j += x;
				}
			}
			j = 0;
			i++;
		}

		return penalty;
	}
}
