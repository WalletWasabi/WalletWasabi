namespace WalletWasabi.Tests.Helpers;

public static class RandomExtensions
{
	public static double Gaussian(this Random random, double mean, double stddev)
	{
		var x1 = 1 - random.NextDouble();
		var x2 = 1 - random.NextDouble();

		var y1 = Math.Sqrt(-2.0 * Math.Log(x1)) * Math.Cos(2.0 * Math.PI * x2);
		return (y1 * stddev) + mean;
	}
}
