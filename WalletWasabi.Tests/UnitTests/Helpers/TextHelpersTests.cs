using System.Globalization;
using WalletWasabi.Fluent.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Helpers;

/// <summary>
/// Tests for <see cref="TextHelpers"/>
/// </summary>
public class TextHelpersTests
{
	[Theory]
	[InlineData("0.01", "+1.0%")]
	[InlineData("0.015", "+1.5%")]
	[InlineData("0.0157", "+1.6%")]
	[InlineData("0.16", "+16%")]
	[InlineData("0.166", "+17%")]
	[InlineData("0.173", "+17%")]
	[InlineData("-0.01", "-1.0%")]
	[InlineData("0.001", "+0.10%")]
	[InlineData("0.000001", "less than +0.01%")]
	[InlineData("-0.000001", "less than -0.01%")]
	public void TestPercentageDiffFormatting(string s, string expected)
	{
		var n = decimal.Parse(s, CultureInfo.InvariantCulture);

		var toString = TextHelpers.FormatPercentageDiff(n);
		Assert.Equal(expected, toString);
	}
}
