using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class StringNoWhiteSpaceEqualityComparerTests
{
	[Theory]
	[InlineData("    a", "a    ", true)]
	[InlineData("    a", "b    ", false)]
	[InlineData("    a", "a\r\t\n    ", true)]
	[InlineData("    a", "b\r\t\n    ", false)]
	[InlineData("", "", true)]
	[InlineData(null, null, true)]
	[InlineData(null, "", false)]
	public void StringNoWhiteSpaceEqualityComparerTest(string? a, string? b, bool equal)
	{
		if (equal)
		{
			Assert.Equal(a, b, new StringNoWhiteSpaceEqualityComparer());
			Assert.Equal(b, a, new StringNoWhiteSpaceEqualityComparer());
		}
		else
		{
			Assert.NotEqual(a, b, new StringNoWhiteSpaceEqualityComparer());
			Assert.NotEqual(b, a, new StringNoWhiteSpaceEqualityComparer());
		}
	}
}
