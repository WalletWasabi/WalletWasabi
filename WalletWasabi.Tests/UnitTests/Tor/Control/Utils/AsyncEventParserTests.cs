using System.Threading.Tasks;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages;
using WalletWasabi.Tor.Control.Messages.Events;
using WalletWasabi.Tor.Control.Messages.Events.StatusEvents;
using WalletWasabi.Tor.Control.Utils;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Control.Utils;

/// <summary>
/// Tests for <see cref="AsyncEventParser"/> class.
/// </summary>
public class AsyncEventParserTests
{
	[Fact]
	public async Task ParseUnknownEventAsync()
	{
		// "XXX" is not a valid event name.
		string data = "650 XXX NOTICE\r\n";

		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		Assert.Throws<NotSupportedException>(() => AsyncEventParser.Parse(rawReply));
	}

	[Fact]
	public async Task ParseSystemClientEventAsync()
	{
		string data = "650 STATUS_CLIENT NOTICE BOOTSTRAP PROGRESS=14 TAG=handshake SUMMARY=\"Handshaking with a relay\"\r\n";

		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		IAsyncEvent asyncEvent = AsyncEventParser.Parse(rawReply);

		BootstrapStatusEvent @event = Assert.IsType<BootstrapStatusEvent>(asyncEvent);
		Assert.NotNull(@event);
	}

	[Fact]
	public async Task ParseCircEventAsync()
	{
		string data = "650 CIRC 16 LAUNCHED BUILD_FLAGS=NEED_CAPACITY PURPOSE=GENERAL TIME_CREATED=2021-06-10T05:42:43.808915\r\n";

		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		IAsyncEvent asyncEvent = AsyncEventParser.Parse(rawReply);

		CircEvent @event = Assert.IsType<CircEvent>(asyncEvent);
		Assert.NotNull(@event);
	}

	[Fact]
	public async Task ParseStreamEventAsync()
	{
		string data = "650 STREAM 35 SUCCEEDED 34 103.47.192.15:8333 SOCKS_USERNAME=\"7NO8P91J0W7TLBS3JTP9F\" SOCKS_PASSWORD=\"7NO8P91J0W7TLBS3JTP9F\" CLIENT_PROTOCOL=SOCKS5 NYM_EPOCH=0 SESSION_GROUP=-4 ISO_FIELDS=SOCKS_USERNAME,SOCKS_PASSWORD,CLIENTADDR,SESSION_GROUP,NYM_EPOCH\r\n";

		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		IAsyncEvent asyncEvent = AsyncEventParser.Parse(rawReply);

		StreamEvent @event = Assert.IsType<StreamEvent>(asyncEvent);
		Assert.NotNull(@event);
	}

	[Fact]
	public async Task BootstrapEvent_ParsesBootstrappedTorAsync()
	{
		string data = "250-status/bootstrap-phase=NOTICE BOOTSTRAP PROGRESS=100 TAG=done SUMMARY=\"Done\"\r\n250 OK\r\n";
		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);

		GetInfoBootstrapPhaseReply reply = GetInfoBootstrapPhaseReply.FromReply(rawReply);

		Assert.Equal(100, reply.Progress);
		Assert.Equal("done", reply.Tag);
		Assert.Equal("Done", reply.Summary);
		Assert.True(reply.IsBootstrapped);
	}

	[Fact]
	public async Task BootstrapEvent_ParsesBootstrappingTorWithAWarningAsync()
	{
		string data = "250-status/bootstrap-phase=WARN BOOTSTRAP PROGRESS=45 TAG=requesting_descriptors SUMMARY=\"Asking for relay descriptors\" WARNING=\"Connection refused\" REASON=CONNECTREFUSED COUNT=3 RECOMMENDATION=warn\r\n250 OK\r\n";
		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);

		GetInfoBootstrapPhaseReply reply = GetInfoBootstrapPhaseReply.FromReply(rawReply);

		Assert.Equal(45, reply.Progress);
		Assert.Equal("requesting_descriptors", reply.Tag);
		Assert.Equal("Asking for relay descriptors", reply.Summary);
		Assert.False(reply.IsBootstrapped);
	}

	[Fact]
	public async Task BootstrapEvent_ParsesWarningWithEscapedQuotesAsync()
	{
		string data = "250-status/bootstrap-phase=WARN BOOTSTRAP PROGRESS=10 TAG=conn_done SUMMARY=\"Connected to a relay\" WARNING=\"TLS error: \\\"bad certificate\\\"\" REASON=TLS_ERROR COUNT=1 RECOMMENDATION=warn\r\n250 OK\r\n";
		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);

		GetInfoBootstrapPhaseReply reply = GetInfoBootstrapPhaseReply.FromReply(rawReply);

		Assert.Equal(10, reply.Progress);
		Assert.Equal("conn_done", reply.Tag);
		Assert.Equal("Connected to a relay", reply.Summary);
	}

	[Fact]
	public async Task BootstrapEvent_RejectsRepliesWithoutProgressAsync()
	{
		TorControlReply noProgress = await TorControlReplyReaderTest.ParseAsync("250-status/bootstrap-phase=NOTICE BOOTSTRAP TAG=done\r\n250 OK\r\n");
		Assert.Throws<TorControlReplyParseException>(() => GetInfoBootstrapPhaseReply.FromReply(noProgress));

		TorControlReply otherKey = await TorControlReplyReaderTest.ParseAsync("250-version=0.4.9.13\r\n250 OK\r\n");
		Assert.Throws<TorControlReplyParseException>(() => GetInfoBootstrapPhaseReply.FromReply(otherKey));

		TorControlReply error = await TorControlReplyReaderTest.ParseAsync("552 Unrecognized key \"status/bootstrap-phase\"\r\n");
		Assert.Throws<TorControlReplyParseException>(() => GetInfoBootstrapPhaseReply.FromReply(error));
	}
}
