using System.Threading.Tasks;
using WalletWasabi.Tor.Control.Messages;
using WalletWasabi.Tor.Control.Messages.Events;
using WalletWasabi.Tor.Control.Messages.StreamStatus;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Control.Messages.Events;

/// <summary>
/// Tests for <see cref="StreamEvent"/> class.
/// </summary>
public class StreamEventTests
{
	[Fact]
	public async Task ParseStreamEventAsync()
	{
		string data = "650 STREAM 38 SENTCONNECT 36 94.204.133.221:8333 SOCKS_USERNAME=\"0QAAN5PBSOM5OXCTPXI0J\" SOCKS_PASSWORD=\"0QAAN5PBSOM5OXCTPXI0J\" CLIENT_PROTOCOL=SOCKS5 NYM_EPOCH=0 SESSION_GROUP=-4 ISO_FIELDS=SOCKS_USERNAME,SOCKS_PASSWORD,CLIENTADDR,SESSION_GROUP,NYM_EPOCH\r\n";

		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		StreamEvent streamEvent = StreamEvent.FromReply(rawReply);

		Assert.NotNull(streamEvent.StreamInfo);

		StreamInfo info = streamEvent.StreamInfo;
		Assert.Equal("38", info.StreamID);
		Assert.Equal(StreamStatusFlag.SENTCONNECT, info.StreamStatus);
		Assert.Equal("36", info.CircuitID);
		Assert.Equal("94.204.133.221", info.TargetAddress);
		Assert.Equal(8333, info.Port);
		Assert.Null(info.Source);
		Assert.Null(info.Purpose);
		Assert.Equal("0QAAN5PBSOM5OXCTPXI0J", info.UserName);
		Assert.Equal("0QAAN5PBSOM5OXCTPXI0J", info.UserPassword);
		Assert.Equal(ClientProtocol.SOCKS5, info.ClientProtocol);
		Assert.Equal(0, info.NymEpoch);
		Assert.Equal(-4, info.SessionGroup);
		Assert.Equal(new IsoFieldFlag[] { IsoFieldFlag.SOCKS_USERNAME, IsoFieldFlag.SOCKS_PASSWORD, IsoFieldFlag.CLIENTADDR, IsoFieldFlag.SESSION_GROUP, IsoFieldFlag.NYM_EPOCH }, info.IsoFields);
	}

	[Fact]
	public async Task ParseStreamEvent2Async()
	{
		string data = "650 STREAM 34 NEW 0 175.32.128.126:8333 SOURCE_ADDR=127.0.0.1:25180 PURPOSE=USER SOCKS_USERNAME=\"YF98TC0IZD4PNMXE2RH0V\" SOCKS_PASSWORD=\"YF98TC0IZD4PNMXE2RH0V\" CLIENT_PROTOCOL=SOCKS5 NYM_EPOCH=0 SESSION_GROUP=-4 ISO_FIELDS=SOCKS_USERNAME,SOCKS_PASSWORD,CLIENTADDR,SESSION_GROUP,NYM_EPOCH\r\n";

		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		StreamEvent streamEvent = StreamEvent.FromReply(rawReply);

		Assert.NotNull(streamEvent.StreamInfo);

		StreamInfo info = streamEvent.StreamInfo;
		Assert.Equal("34", info.StreamID);
		Assert.Equal(StreamStatusFlag.NEW, info.StreamStatus);
		Assert.Equal("0", info.CircuitID);
		Assert.Equal("175.32.128.126", info.TargetAddress);
		Assert.Equal(8333, info.Port);
		Assert.Equal("127.0.0.1:25180", info.SourceAddr);
		Assert.Equal(Purpose.USER, info.Purpose);
		Assert.Equal("YF98TC0IZD4PNMXE2RH0V", info.UserName);
		Assert.Equal("YF98TC0IZD4PNMXE2RH0V", info.UserPassword);
		Assert.Equal(ClientProtocol.SOCKS5, info.ClientProtocol);
		Assert.Equal(0, info.NymEpoch);
		Assert.Equal(-4, info.SessionGroup);
		Assert.Equal(new IsoFieldFlag[] { IsoFieldFlag.SOCKS_USERNAME, IsoFieldFlag.SOCKS_PASSWORD, IsoFieldFlag.CLIENTADDR, IsoFieldFlag.SESSION_GROUP, IsoFieldFlag.NYM_EPOCH }, info.IsoFields);
	}

	[Fact]
	public async Task ParseStreamEvent3Async()
	{
		string data = "650 STREAM 69 NEW 0 138.201.196.252.$AC7C88D67339B90F2C10FF7B702DCB1EF3B8D663.exit:9993 PURPOSE=DIR_FETCH CLIENT_PROTOCOL=UNKNOWN NYM_EPOCH=0 SESSION_GROUP=-2 ISO_FIELDS=\r\n";

		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		StreamEvent streamEvent = StreamEvent.FromReply(rawReply);

		Assert.NotNull(streamEvent.StreamInfo);

		StreamInfo info = streamEvent.StreamInfo;
		Assert.Equal("69", info.StreamID);
		Assert.Equal(StreamStatusFlag.NEW, info.StreamStatus);
		Assert.Equal("0", info.CircuitID);
		Assert.Equal("138.201.196.252.$AC7C88D67339B90F2C10FF7B702DCB1EF3B8D663.exit", info.TargetAddress);
		Assert.Equal(9993, info.Port);
		Assert.Null(info.SourceAddr);
		Assert.Equal(Purpose.DIR_FETCH, info.Purpose);
		Assert.Null(info.UserName);
		Assert.Null(info.UserPassword);
		Assert.Equal(ClientProtocol.UNKNOWN, info.ClientProtocol);
		Assert.Equal(0, info.NymEpoch);
		Assert.Equal(-2, info.SessionGroup);
		Assert.Empty(info.IsoFields);
	}
}
