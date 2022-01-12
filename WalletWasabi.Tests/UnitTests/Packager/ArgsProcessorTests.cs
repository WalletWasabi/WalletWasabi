using WalletWasabi.Packager;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Packager;

/// <summary>
/// Tests for <see cref="ArgsProcessor"/> class.
/// </summary>
public class ArgsProcessorTests
{
	[Theory]
	[InlineData(new string[] { "-onlybinaries" }, true)]
	[InlineData(new string[] { "-onlyBinaries" }, true)]
	[InlineData(new string[] { "-OnlyBinaries" }, true)]
	[InlineData(new string[] { "---OnlyBinaries" }, true)]
	[InlineData(new string[] { "---0nlyBinaries" }, false)]
	public void IsOnlyBinariesModeTest(string[] input, bool expectedResult)
	{
		var argsProcessor = new ArgsProcessor(input);
		Assert.Equal(expectedResult, argsProcessor.IsOnlyBinariesMode());
	}
}
