using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Utils;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Control.Utils;

/// <summary>
/// Tests for <see cref="Tokenizer"/> class.
/// </summary>
public class TokenizerTests
{
	[Fact]
	public void ReadQuotedString()
	{
		// Input without a closing quote.
		{
			// There is a starting quote, but no closing quote.
			var input = """
				"unterminated
				""";

			Assert.Throws<TorControlReplyParseException>(() => Tokenizer.ReadQuotedString(input));

			// There is a starting quote and an escaped quote, but no closing quote.
			input = """
				"unterminated \"
				""";

			Assert.Throws<TorControlReplyParseException>(() => Tokenizer.ReadQuotedString(input));
		}

		// Read a quoted string with escaped quotes properly.
		{
			var input = """
				"TLS error: \"bad certificate\"" REASON=TLS_ERROR
				""";

			(var actualQuotedString, var remainder) = Tokenizer.ReadQuotedString(input);

			var expectedQuotedString = """
				TLS error: "bad certificate"
				""";
			Assert.Equal(expectedQuotedString, actualQuotedString);
			Assert.Equal(" REASON=TLS_ERROR", remainder);
		}
	}

	[Fact]
	public void ReadKeyValueAssignment()
	{
		var input = """WARNING="a \"b\" c" COUNT=3""";
		(var key, var value, var remainder) = Tokenizer.ReadKeyValueAssignment(input, allowValueAsQuotedString: true);

		Assert.Equal("WARNING", key);
		Assert.Equal("a \"b\" c", value);

		// The separator after a quoted value is left in the remainder.
		Assert.Equal(" COUNT=3", remainder);
	}
}
