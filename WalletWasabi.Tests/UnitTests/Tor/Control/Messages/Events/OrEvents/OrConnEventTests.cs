using System.Threading.Tasks;
using WalletWasabi.Tor.Control.Messages;
using WalletWasabi.Tor.Control.Messages.Events.OrEvents;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Control.Messages.Events.OrEvents;

/// <summary>
/// Tests for <see cref="OrConnEvent"/> class.
/// </summary>
public class OrConnEventTests
{
	[Fact]
	public async Task ParseOrConnEventAsync()
	{
		string data = "650 ORCONN $A1B28D636A56AAFFE92ADCCA937AA4BD5333BB4C~bakunin4 LAUNCHED ID=5\r\n";

		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		OrConnEvent orConnEvent = OrConnEvent.FromReply(rawReply);

		Assert.Equal("$A1B28D636A56AAFFE92ADCCA937AA4BD5333BB4C~bakunin4", orConnEvent.Target);
		Assert.Equal(OrStatus.LAUNCHED, orConnEvent.OrStatus);
		Assert.Null(orConnEvent.NCircs);
		Assert.Equal("5", orConnEvent.ConnId);
	}
}
