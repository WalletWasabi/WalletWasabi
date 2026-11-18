using System.Threading.Tasks;
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

		var rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		Assert.Throws<NotSupportedException>(() => AsyncEventParser.Parse(rawReply));
	}

	[Fact]
	public async Task ParseSystemClientEventAsync()
	{
		string data = "650 STATUS_CLIENT NOTICE BOOTSTRAP PROGRESS=14 TAG=handshake SUMMARY=\"Handshaking with a relay\"\r\n";

		var rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		var asyncEvent = AsyncEventParser.Parse(rawReply);

		var @event = Assert.IsType<BootstrapStatusEvent>(asyncEvent);
		Assert.NotNull(@event);
	}

	[Fact]
	public async Task ParseCircEventAsync()
	{
		string data = "650 CIRC 16 LAUNCHED BUILD_FLAGS=NEED_CAPACITY PURPOSE=GENERAL TIME_CREATED=2021-06-10T05:42:43.808915\r\n";

		var rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		var asyncEvent = AsyncEventParser.Parse(rawReply);

		var @event = Assert.IsType<CircEvent>(asyncEvent);
		Assert.NotNull(@event);
	}

	[Fact]
	public async Task ParseStreamEventAsync()
	{
		string data = "650 STREAM 35 SUCCEEDED 34 103.47.192.15:8333 SOCKS_USERNAME=\"7NO8P91J0W7TLBS3JTP9F\" SOCKS_PASSWORD=\"7NO8P91J0W7TLBS3JTP9F\" CLIENT_PROTOCOL=SOCKS5 NYM_EPOCH=0 SESSION_GROUP=-4 ISO_FIELDS=SOCKS_USERNAME,SOCKS_PASSWORD,CLIENTADDR,SESSION_GROUP,NYM_EPOCH\r\n";

		var rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		var asyncEvent = AsyncEventParser.Parse(rawReply);

		var @event = Assert.IsType<StreamEvent>(asyncEvent);
		Assert.NotNull(@event);
	}


	[Fact]
	public async Task ParseBootstrapEventsAsync()
	{
		BootstrapTestCase[] bootstrapEvents = [
			new("650 STATUS_CLIENT NOTICE BOOTSTRAP PROGRESS=5 TAG=conn SUMMARY=\"Connecting to a relay\"\r\n", 5, "conn", "Connecting to a relay"),
			new("650 STATUS_CLIENT NOTICE BOOTSTRAP PROGRESS=10 TAG=conn_done SUMMARY=\"Connected to a relay\"\r\n", 10, "conn_done", "Connected to a relay"),
			new("650 STATUS_CLIENT NOTICE BOOTSTRAP PROGRESS=14 TAG=handshake SUMMARY=\"Handshaking with a relay\"\r\n", 14, "handshake", "Handshaking with a relay"),
			new("650 STATUS_CLIENT NOTICE BOOTSTRAP PROGRESS=15 TAG=handshake_done SUMMARY=\"Handshake with a relay done\"\r\n", 15, "handshake_done", "Handshake with a relay done"),
			new("650 STATUS_CLIENT NOTICE BOOTSTRAP PROGRESS=75 TAG=enough_dirinfo SUMMARY=\"Loaded enough directory info to build circuits\"\r\n", 75, "enough_dirinfo", "Loaded enough directory info to build circuits"),
			new("650 STATUS_CLIENT NOTICE BOOTSTRAP PROGRESS=90 TAG=ap_handshake_done SUMMARY=\"Handshake finished with a relay to build circuits\"\r\n", 90, "ap_handshake_done", "Handshake finished with a relay to build circuits"),
			new("650 STATUS_CLIENT NOTICE BOOTSTRAP PROGRESS=95 TAG=circuit_create SUMMARY=\"Establishing a Tor circuit\"\r\n", 95, "circuit_create", "Establishing a Tor circuit"),
			new("650 STATUS_CLIENT NOTICE BOOTSTRAP PROGRESS=100 TAG=done SUMMARY=\"Done\"\r\n", 100, "done", "Done"),
		];

		foreach (var @event in bootstrapEvents)
		{
			var rawReply = await TorControlReplyReaderTest.ParseAsync(@event.ReceivedMessage);
			var asyncEvent = AsyncEventParser.Parse(rawReply);

			var bootstrapEvent = Assert.IsType<BootstrapStatusEvent>(asyncEvent);
			Assert.Equal(bootstrapEvent.Progress, bootstrapEvent.Progress);
			Assert.Equal(bootstrapEvent.Tag, bootstrapEvent.Tag);
			Assert.Equal(bootstrapEvent.Summary, bootstrapEvent.Summary);
		}

		// Final status event: Tor built its first circuit.
		var circuitEstablishedRawReply = await TorControlReplyReaderTest.ParseAsync("650 STATUS_CLIENT NOTICE CIRCUIT_ESTABLISHED\r\n");
		var circuitEstablishedEvent = AsyncEventParser.Parse(circuitEstablishedRawReply);

		Assert.NotNull(circuitEstablishedEvent);
	}
}

/// <param name="ReceivedMessage">Received bootstrap event received over Tor control protocol.</param>
file record struct BootstrapTestCase(string ReceivedMessage, int ExpectedProgress, string ExpectedTag, string ExpectedSummary);
