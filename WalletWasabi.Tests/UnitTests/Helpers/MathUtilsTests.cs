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
}
