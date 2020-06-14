using System.Linq;

namespace Gma.QrCodeNet.Encoding.Masking.Scoring
{
	internal static class MatrixScoreCalculator
	{
		internal static BitMatrix GetLowestPenaltyMatrix(this TriStateMatrix matrix, ErrorCorrectionLevel errorLevel)
		{
			PatternFactory patternFactory = new PatternFactory();
			int score = int.MaxValue;
			int tempScore;
			TriStateMatrix result = new TriStateMatrix(matrix.Width);
			TriStateMatrix triMatrix;
			foreach (Pattern pattern in patternFactory.AllPatterns())
			{
				triMatrix = matrix.Apply(pattern, errorLevel);
				tempScore = triMatrix.PenaltyScore();
				if (tempScore < score)
				{
					score = tempScore;
					result = triMatrix;
				}
			}

			return result;
		}

		internal static int PenaltyScore(this BitMatrix matrix)
		{
			PenaltyFactory penaltyFactory = new PenaltyFactory();
			return
				penaltyFactory
				.AllRules()
				.Sum(penalty => penalty.PenaltyCalculate(matrix));
		}
	}
}
