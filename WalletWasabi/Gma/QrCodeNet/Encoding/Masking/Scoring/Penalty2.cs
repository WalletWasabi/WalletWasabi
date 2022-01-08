namespace Gma.QrCodeNet.Encoding.Masking.Scoring;

/// <summary>
/// ISO/IEC 18004:2000 Chapter 8.8.2 Page 52
/// </summary>
internal class Penalty2 : Penalty
{
	internal override int PenaltyCalculate(BitMatrix matrix)
	{
		int width = matrix.Width;
		int x = 0;
		int y = 0;
		int penalty = 0;

		while (y < (width - 1))
		{
			while (x < (width - 1))
			{
				bool topR = matrix[x + 1, y];

				if (topR == matrix[x + 1, y + 1]) // Bottom Right
				{
					if (topR == matrix[x, y + 1]) // Bottom Left
					{
						if (topR == matrix[x, y]) // Top Left
						{
							penalty += 3;
							x += 1;
						}
						else
						{
							x += 1;
						}
					}
					else
					{
						x += 1;
					}
				}
				else
				{
					x += 2;
				}
			}

			x = 0;
			y++;
		}
		return penalty;
	}
}
