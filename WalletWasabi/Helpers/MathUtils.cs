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

	public static int CountDecimalPlaces(this decimal n)
	{
		return BitConverter.GetBytes(decimal.GetBits(n)[3])[2];
	}
}
