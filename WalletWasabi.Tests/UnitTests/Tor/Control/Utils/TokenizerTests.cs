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
	public void ReadQuotedStringSkipsEscapedQuotes()
	{
		(string value, string remainder) = Tokenizer.ReadQuotedString("\"TLS error: \\\"bad certificate\\\"\" REASON=TLS_ERROR");

		Assert.Equal("TLS error: \"bad certificate\"", value);
		Assert.Equal(" REASON=TLS_ERROR", remainder);
	}

	[Fact]
	public void ReadQuotedStringHandlesEscapedQuoteAtTheEnd()
	{
		(string value, string remainder) = Tokenizer.ReadQuotedString("\"say \\\"hi\\\"\"");

		Assert.Equal("say \"hi\"", value);
		Assert.Equal("", remainder);
	}

	[Fact]
	public void ReadQuotedStringRejectsMissingClosingQuote()
	{
		Assert.Throws<TorControlReplyParseException>(() => Tokenizer.ReadQuotedString("\"unterminated \\\""));
		Assert.Throws<TorControlReplyParseException>(() => Tokenizer.ReadQuotedString("\"unterminated"));
	}

	[Fact]
	public void ReadKeyValueAssignmentReadsQuotedValuesWithEscapedQuotes()
	{
		(string key, string value, string remainder) = Tokenizer.ReadKeyValueAssignment("WARNING=\"a \\\"b\\\" c\" COUNT=3", allowValueAsQuotedString: true);

		Assert.Equal("WARNING", key);
		Assert.Equal("a \"b\" c", value);

		// The separator after a quoted value is left in the remainder.
		Assert.Equal(" COUNT=3", remainder);
	}
}
