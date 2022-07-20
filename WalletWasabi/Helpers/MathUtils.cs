namespace WalletWasabi.Helpers;

public static class MathUtils
{
	public static int Round(int n, int precision)
	{
		var fraction = n / (double)precision;
		var roundedFraction = Math.Round(fraction);
		var rounded = roundedFraction * precision;
		return (int)rounded;
	}

	public static decimal RoundToSignificantFigures(this decimal num, int n)
	{
		if (num == 0)
		{
			return 0;
		}

		int d = (int)Math.Ceiling(Math.Log10((double)Math.Abs(num)));
		int power = n - d;

		decimal magnitude = (decimal)Math.Pow(10, power);

		decimal shifted = Math.Round(num * magnitude, 0, MidpointRounding.AwayFromZero);
		decimal ret = shifted / magnitude;

		return ret;
	}
}
