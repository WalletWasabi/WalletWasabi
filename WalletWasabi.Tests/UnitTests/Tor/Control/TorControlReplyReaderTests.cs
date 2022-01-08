using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Control;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Control;

/// <summary>
/// Tests for <see cref="TorControlReplyReader"/> class.
/// </summary>
public class TorControlReplyReaderTest
{
	[Theory]
	[InlineData(StatusCode.OK, 1, "250 OK\r\n")]
	[InlineData(StatusCode.OK, 2, "250-SOCKSPORT=37150\r\n250 ORPORT=0\r\n")]
	[InlineData(StatusCode.OK, 4, "250-PROTOCOLINFO 1\r\n250-AUTH METHODS=HASHEDPASSWORD\r\n250-VERSION Tor=\"0.4.3.5\"\r\n250 OK\r\n")]
	[InlineData(StatusCode.AsynchronousEventNotify, 1, "650 CIRC 1000 EXTENDED moria1,moria2\r\n")]
	public async Task WellformedTestsAsync(StatusCode expectedStatusCode, int expectedReplyLines, string data)
	{
		TorControlReply reply = await ParseAsync(data);

		if (expectedStatusCode == StatusCode.OK)
		{
			Assert.True(reply.Success);
		}

		Assert.Equal(expectedStatusCode, reply.StatusCode);
		Assert.Equal(expectedReplyLines, reply.ResponseLines.Count);
	}

	/// <summary>Makes sure we don't normalize <c>\n</c>, <c>\t</c>, <c>\r</c> in Tor replies.</summary>
	/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">2.1.1. Notes on an escaping bug</seealso>
	[Fact]
	public async Task EscapingQuotesTestAsync()
	{
		TorControlReply reply = await ParseAsync("250-PROTOCOLINFO 1\r\n250-VERSION Tor=\\\"0.4.3.5\\\"\r\n250 OK\r\n");

		Assert.True(reply.Success);
		Assert.Equal(3, reply.ResponseLines.Count);

		// We are expected to read: 'VERSION Tor=\"0.4.3.5\"' (with the backslashes).
		Assert.Equal(@"VERSION Tor=\""0.4.3.5\""", reply.ResponseLines[1]);
	}

	[Fact]
	public async Task InvalidStatusCodeAsync()
	{
		var ex = await Assert.ThrowsAsync<TorControlReplyParseException>(async () => await ParseAsync("2 OK\r\n").ConfigureAwait(false));
		Assert.StartsWith("Unknown status code: '2 O'", ex.Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("No reply line was received.", "\r" /* no input line */)]
	[InlineData("Status code requires at least 3 characters.", "OK\r\n")]
	[InlineData("Unknown status code: 'xx '.", "xx OK\r\n")]
	[InlineData("Unknown status code: '2 O'.", "2 OK\r\n")]
	public async Task InvalidRepliesAsync(string expectedExceptionMsg, string data)
	{
		var ex = await Assert.ThrowsAsync<TorControlReplyParseException>(async () => await ParseAsync(data).ConfigureAwait(false));
		Assert.Equal(expectedExceptionMsg, ex.Message);
	}

	public static async Task<TorControlReply> ParseAsync(string data)
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(1));

		Pipe pipe = new();
		await pipe.Writer.WriteAsciiAndFlushAsync(data, timeoutCts.Token);
		await pipe.Writer.CompleteAsync();
		return await TorControlReplyReader.ReadReplyAsync(pipe.Reader, timeoutCts.Token);
	}
}
