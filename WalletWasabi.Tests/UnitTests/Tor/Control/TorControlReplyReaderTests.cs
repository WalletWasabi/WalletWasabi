using System;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Control;
using WalletWasabi.Tor.Control.Messages;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Control
{
	/// <summary>
	/// Tests for <see cref="TorControlReplyReader"/> class.
	/// </summary>
	public class TorControlReplyReaderTest
	{
		/// <summary>
		/// TODO.
		/// </summary>
		[Theory]
		[InlineData(StatusCode.OK, 1, "250 OK\r\n")]
		[InlineData(StatusCode.OK, 2, "250-SOCKSPORT=9050\r\n250 ORPORT=0\r\n")]
		[InlineData(StatusCode.OK, 4, "250-PROTOCOLINFO 1\r\n250-AUTH METHODS=HASHEDPASSWORD\r\n250-VERSION Tor=\"0.4.3.5\"\r\n250 OK\r\n")]
		[InlineData(StatusCode.AsynchronousEventNotify, 1, "650 CIRC 1000 EXTENDED moria1,moria2\r\n")]
		public async Task ReplyParsingTestsAsync(StatusCode expectedStatusCode, int expectedReplyLines, string data)
		{		
			using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(1));

			Pipe pipe = new();
			await pipe.Writer.WriteAsync(data, Encoding.ASCII, timeoutCts.Token);
			TorControlReply reply = await TorControlReplyReader.ReadReplyAsync(pipe.Reader, timeoutCts.Token);

			if (expectedStatusCode == StatusCode.OK)
			{
				Assert.True(reply.Success);
			}

			Assert.Equal(expectedStatusCode, reply.StatusCode);
			Assert.Equal(expectedReplyLines, reply.ResponseLines.Count);			
		}
	}

	public static class PipeWriterExtensions
	{
		public static ValueTask<FlushResult> WriteAsync(this PipeWriter writer, string data, Encoding encoding, CancellationToken cancellationToken = default)
		{
			return writer.WriteAsync(new ReadOnlyMemory<byte>(encoding.GetBytes(data)), cancellationToken);
		}

		public static ValueTask<FlushResult> WriteAsciiAsync(this PipeWriter writer, string data, CancellationToken cancellationToken = default)
		{
			return writer.WriteAsync(new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes(data)), cancellationToken);
		}
	}
}