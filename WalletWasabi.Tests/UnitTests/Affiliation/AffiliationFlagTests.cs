using WalletWasabi.Affiliation;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Affiliation;

public class AffiliationFlagTests
{
	[Theory]
	[InlineData("1")]
	[InlineData("a")]
	[InlineData("A")]
	[InlineData("a1")]
	[InlineData("A1")]
	[InlineData("?")]
	[InlineData("$")]
	[InlineData("12345678901234567890")] // 20 characters.
	public void ValidNameTest(string input)
	{
		AffiliationFlag flag = new(input);
		Assert.NotNull(flag);
	}

	[Theory]
	[InlineData("")]
	[InlineData("123456789012345678901")] // 21 characters.
	[InlineData("müller")] // non-ASCII character.
	[InlineData("MÜLLER")] // non-ASCII character.
	public void InvalidNameTest(string input)
	{
		Assert.Throws<ArgumentException>(() => new AffiliationFlag(input));
	}
}
