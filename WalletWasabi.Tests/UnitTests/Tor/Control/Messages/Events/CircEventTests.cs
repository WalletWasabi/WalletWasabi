using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Tor.Control.Messages;
using WalletWasabi.Tor.Control.Messages.CircuitStatus;
using WalletWasabi.Tor.Control.Messages.Events;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Control.Messages.Events;

/// <summary>
/// Tests for <see cref="CircEvent"/> class.
/// </summary>
/// <remarks>
/// More tests can be found in <see cref="GetInfoCircuitStatusReplyTests"/> as
/// <see cref="GetInfoCircuitStatusReply"/> message is parsed with a similar parser.
/// </remarks>
public class CircEventTests
{
	[Fact]
	public async Task ParseCircEvent1Async()
	{
		string data = "650 CIRC 5 EXTENDED $51BD782616C3EBA543B0D4EE34D7C1CE1ED2291D~Geodude BUILD_FLAGS=NEED_CAPACITY PURPOSE=GENERAL TIME_CREATED=2021-06-10T06:26:32.440036 SOCKS_USERNAME=\"AXNH6A3AVT9863JK4VNWD\" SOCKS_PASSWORD=\"AXNH6A3AVT9863JK4VNWD\"\r\n";

		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		CircEvent circEvent = CircEvent.FromReply(rawReply);

		Assert.NotNull(circEvent.CircuitInfo);

		CircuitInfo info = circEvent.CircuitInfo;
		Assert.Equal("5", info.CircuitID);
		Assert.Equal(CircStatus.EXTENDED, info.CircStatus);

		CircPath circPath = Assert.Single(info.CircPaths);
		Assert.Equal("$51BD782616C3EBA543B0D4EE34D7C1CE1ED2291D", circPath.FingerPrint);
		Assert.Equal("Geodude", circPath.Nickname);

		Assert.Equal(new List<BuildFlag>() { BuildFlag.NEED_CAPACITY }, info.BuildFlags);
		Assert.Equal(Purpose.GENERAL, info.Purpose);
		Assert.Equal("2021-06-10T06:26:32.440036", info.TimeCreated);
		Assert.Equal("AXNH6A3AVT9863JK4VNWD", info.UserName);
		Assert.Equal("AXNH6A3AVT9863JK4VNWD", info.UserPassword);
	}

	[Fact]
	public async Task ParseCircEvent2Async()
	{
		string data = "650 CIRC 16 LAUNCHED BUILD_FLAGS=NEED_CAPACITY PURPOSE=GENERAL TIME_CREATED=2021-06-10T05:42:43.808915\r\n";

		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		CircEvent circEvent = CircEvent.FromReply(rawReply);

		Assert.NotNull(circEvent.CircuitInfo);

		CircuitInfo info = circEvent.CircuitInfo;
		Assert.Equal("16", info.CircuitID);
		Assert.Equal(CircStatus.LAUNCHED, info.CircStatus);
		Assert.Equal(new List<BuildFlag>() { BuildFlag.NEED_CAPACITY }, info.BuildFlags);
		Assert.Equal(Purpose.GENERAL, info.Purpose);
		Assert.Equal("2021-06-10T05:42:43.808915", info.TimeCreated);
	}
}
