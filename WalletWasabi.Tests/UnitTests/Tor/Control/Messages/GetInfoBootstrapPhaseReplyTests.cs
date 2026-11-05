using System.Threading.Tasks;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Control.Messages;

/// <summary>
/// Tests for <see cref="GetInfoBootstrapPhaseReply"/> class.
/// </summary>
public class GetInfoBootstrapPhaseReplyTests
{
	[Fact]
	public async Task ParsesABootstrappedTorAsync()
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
	public async Task ParsesABootstrappingTorWithAWarningAsync()
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
	public async Task RejectsRepliesWithoutProgressAsync()
	{
		TorControlReply noProgress = await TorControlReplyReaderTest.ParseAsync("250-status/bootstrap-phase=NOTICE BOOTSTRAP TAG=done\r\n250 OK\r\n");
		Assert.Throws<TorControlReplyParseException>(() => GetInfoBootstrapPhaseReply.FromReply(noProgress));

		TorControlReply otherKey = await TorControlReplyReaderTest.ParseAsync("250-version=0.4.9.13\r\n250 OK\r\n");
		Assert.Throws<TorControlReplyParseException>(() => GetInfoBootstrapPhaseReply.FromReply(otherKey));

		TorControlReply error = await TorControlReplyReaderTest.ParseAsync("552 Unrecognized key \"status/bootstrap-phase\"\r\n");
		Assert.Throws<TorControlReplyParseException>(() => GetInfoBootstrapPhaseReply.FromReply(error));
	}
}
