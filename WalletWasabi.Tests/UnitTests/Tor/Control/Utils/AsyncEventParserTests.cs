using System.Threading.Tasks;
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
}
