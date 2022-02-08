using System.Threading.Tasks;
using WalletWasabi.Tor.Control.Messages;
using WalletWasabi.Tor.Control.Messages.Events.StatusEvents;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Control.Messages.Events.StatusEvents;

/// <summary>
/// Tests for <see cref="StatusEvent"/> class.
/// </summary>
public class StatusEventTests
{
	[Fact]
	public async Task ParseBootstrapStatusEvent1Async()
	{
		string data = "650 STATUS_CLIENT NOTICE BOOTSTRAP PROGRESS=14 TAG=handshake SUMMARY=\"Handshaking with a relay\"\r\n";

		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		StatusEvent statusEvent = StatusEvent.FromReply(rawReply);

		Assert.Equal(StatusType.STATUS_CLIENT, statusEvent.Type);
		Assert.Equal(StatusSeverity.NOTICE, statusEvent.Severity);
		Assert.Equal("BOOTSTRAP", statusEvent.Action);
		Assert.Equal(3, statusEvent.Arguments.Count);
		Assert.Equal("14", statusEvent.Arguments["PROGRESS"]);
		Assert.Equal("handshake", statusEvent.Arguments["TAG"]);
		Assert.Equal("Handshaking with a relay", statusEvent.Arguments["SUMMARY"]);
	}

	[Fact]
	public async Task ParseBootstrapStatusEvent2Async()
	{
		string data = "650 STATUS_CLIENT NOTICE BOOTSTRAP PROGRESS=95 TAG=circuit_create SUMMARY=\"Establishing a Tor circuit\"\r\n";

		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		StatusEvent statusEvent = StatusEvent.FromReply(rawReply);

		Assert.Equal(StatusType.STATUS_CLIENT, statusEvent.Type);
		Assert.Equal(StatusSeverity.NOTICE, statusEvent.Severity);
		Assert.Equal("BOOTSTRAP", statusEvent.Action);
		Assert.Equal(3, statusEvent.Arguments.Count);
		Assert.Equal("95", statusEvent.Arguments["PROGRESS"]);
		Assert.Equal("circuit_create", statusEvent.Arguments["TAG"]);
		Assert.Equal("Establishing a Tor circuit", statusEvent.Arguments["SUMMARY"]);
	}

	[Fact]
	public async Task ParseCircuitEstablishedStatusEventAsync()
	{
		string data = "650 STATUS_CLIENT NOTICE CIRCUIT_ESTABLISHED\r\n";

		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		StatusEvent statusEvent = StatusEvent.FromReply(rawReply);

		Assert.Equal(StatusType.STATUS_CLIENT, statusEvent.Type);
		Assert.Equal(StatusSeverity.NOTICE, statusEvent.Severity);
		Assert.Equal("CIRCUIT_ESTABLISHED", statusEvent.Action);
		Assert.Empty(statusEvent.Arguments);
	}
}
