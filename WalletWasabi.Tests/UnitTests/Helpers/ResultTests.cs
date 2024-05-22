using System.Linq;
using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Helpers;

using ParsingResult = Result<int, string>;
public class ResultTests
{
	private ParsingResult ParseInt(string s) =>
		int.TryParse(s, out var n)
			? n
			: $"{s} is not a number";

	[Fact]
	public void SimpleResultTests()
	{
		// Simple results
		// "1" -> Ok 1
		// "Maria" -> Fail "Maria is not a number"
		Assert.Equal(ParsingResult.Ok(1), ParseInt("1"));
		Assert.Equal(ParsingResult.Fail("Maria is not a number"), ParseInt("Maria"));

		// ["1", "2", "3"] -> Ok [1, 2, 3]
		new[] { "1", "2", "3" }
			.Select(ParseInt)
			.SequenceResults()
			.MatchDo(
				vs => Assert.Equal(new[]{1,2,3}, vs),
				_ => Assert.Fail("All numbers are correct and it shouldn't have failed."));

		// ["Maria", "Julio"] -> Fail ["Maria is not a number", "Julio is not a number"]
		new[] { "Maria", "Julio" }
			.Select(ParseInt)
			.SequenceResults()
			.MatchDo(
				_ => Assert.Fail("There are no valid integers, this should have failed."),
				e => Assert.Equal(new [] {"Maria is not a number", "Julio is not a number"}, e));

		// [Ok "1", Fail "Maria", Ok -7] -> Fail ["Maria is not a number"]
		new [] { ParseInt("1"), ParseInt("Maria"), ParseInt("-7") }
			.SequenceResults()
			.MatchDo(
				_ => Assert.Fail("There are non-integer values, this should have failed."),
				e => Assert.Equal(new [] {"Maria is not a number"}, e));

		// [Ok "1", Ok -7, Fail "Julio", Fail "Maria",] -> Fail ["Julio is not a number", "Maria is not a number"]
		new [] { ParseInt("1"),  ParseInt("-7"), ParseInt("Julio"), ParseInt("Maria") }
			.SequenceResults()
			.MatchDo(
				_ => Assert.Fail("There are non-integer values, this should have failed."),
				e => Assert.Equal(new [] {"Julio is not a number", "Maria is not a number"}, e));
	}
}
