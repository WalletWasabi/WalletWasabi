using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Helpers;

public class MathUtilsTests
{
	[Theory]
	[InlineData(45, 15, 45)]
	[InlineData(30, 15, 30)]
	[InlineData(31, 15, 30)]
	[InlineData(40, 15, 45)]
	[InlineData(55, 20, 60)]
	public void Round(int actual, int precision, int expected)
	{
		Assert.Equal(expected, MathUtils.Round(actual, precision));
	}

	[Theory]
	[InlineData(92.96, 3, 93)]
	[InlineData(215.80, 3, 216)]
	[InlineData(123, 3, 123)]
	[InlineData(123.5, 3, 124)]
	[InlineData(1234.5, 3, 1230)]
	[InlineData(0.123, 3, 0.123)]
	[InlineData(0.12, 3, 0.12)]
	[InlineData(0.99, 3, 0.99)]
	[InlineData(265.88, 3, 266)]
	[InlineData(1751, 3, 1750)]
	[InlineData(1751, 2, 1800)]
	[InlineData(1751, 1, 2000)]
	public void RoundToSignificantFigures(decimal actual, int precision, decimal expected)
	{
		Assert.Equal(expected, actual.RoundToSignificantFigures(precision));
	}
}
