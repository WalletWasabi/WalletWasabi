namespace WalletWasabi.Fluent.Helpers;

public class MathUtils
{
	public static int Round(int n, int precision)
	{
		var fraction = n / (double)precision;
		var roundedFraction = Math.Round(fraction);
		var rounded = roundedFraction * precision;
		return (int)rounded;
	}
}