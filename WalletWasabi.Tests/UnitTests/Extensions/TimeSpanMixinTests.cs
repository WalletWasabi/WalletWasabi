using System.Globalization;
using WalletWasabi.Fluent.Extensions;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Extensions;

public class TimeSpanMixinTests
{
	[Theory]
	[InlineData("1:0:0", "1:0:0")]  // input and expected are formatted as {days}:{hours}:{minutes}
	[InlineData("1:0:33", "1:0:0")]
	[InlineData("1:1:0", "1:0:0")]
	[InlineData("0:12:0", "0:12:0")]
	[InlineData("1:12:0", "2:0:0")]
	[InlineData("1:13:0", "2:0:0")]
	[InlineData("0:15:59", "0:16:0")]
	[InlineData("0:9:59", "0:10:0")]
	[InlineData("0:0:35", "0:0:35")]
	[InlineData("0:0:20", "0:0:20")]
	[InlineData("0:0:50", "0:0:50")]
	[InlineData("0:1:30", "0:1:30")]
	[InlineData("0:1:1", "0:1:0")]
	[InlineData("0:0:1", "0:0:1")]
	[InlineData("0:0:0", "0:0:0")]
	[InlineData("0:8:40", "0:8:30")]
	[InlineData("0:2:16", "0:2:30")]
	public void Reduce(string inputStr, string expectedStr)
	{
		var input = ParseExact(inputStr);

		var actual = input.Reduce();
		var expected = ParseExact(expectedStr);
		Assert.Equal(expected, actual);
	}

	private static TimeSpan ParseExact(string inputStr)
	{
		return TimeSpan.ParseExact(inputStr, @"d\:h\:m", CultureInfo.InvariantCulture);
	}
}
