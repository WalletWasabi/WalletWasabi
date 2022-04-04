using System.Threading.Tasks;
using WalletWasabi.Tor.Control.Messages;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Control.Messages;

/// <summary>
/// Tests for <see cref="ProtocolInfoReply"/> class.
/// </summary>
public class ProtocolInfoReplyTests
{
	[Fact]
	public async Task AuthMethodHashedPasswordAsync()
	{
		string data = "250-PROTOCOLINFO 1\r\n250-AUTH METHODS=HASHEDPASSWORD\r\n250-VERSION Tor=\"0.4.3.5\"\r\n250 OK\r\n";

		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		ProtocolInfoReply reply = ProtocolInfoReply.FromReply(rawReply);

		Assert.Equal(1, reply.ProtocolVersion);
		Assert.Equal("0.4.3.5", reply.TorVersion);
		Assert.Single(reply.AuthMethods);
		Assert.Contains("HASHEDPASSWORD", reply.AuthMethods);
	}

	[Fact]
	public async Task AuthMethodCookieAsync()
	{
		// Yes, Tor really returns: "C:\\Users\\Wasabi\\AppData\\Roaming\\WalletWasabi\\Client\\control_auth_cookie" path on Windows.
		string data = "250-PROTOCOLINFO 1\r\n250-AUTH METHODS=COOKIE,SAFECOOKIE COOKIEFILE=\"C:\\\\Users\\\\Wasabi\\\\AppData\\\\Roaming\\\\WalletWasabi\\\\Client\\\\control_auth_cookie\"\r\n250-VERSION Tor=\"0.4.5.7\"\r\n250 OK\r\n";

		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		ProtocolInfoReply reply = ProtocolInfoReply.FromReply(rawReply);

		Assert.Equal(1, reply.ProtocolVersion);
		Assert.Equal("0.4.5.7", reply.TorVersion);
		Assert.Equal(2, reply.AuthMethods.Length);
		Assert.Equal("COOKIE", reply.AuthMethods[0]);
		Assert.Equal("SAFECOOKIE", reply.AuthMethods[1]);
		Assert.Equal(@"C:\Users\Wasabi\AppData\Roaming\WalletWasabi\Client\control_auth_cookie", reply.CookieFilePath);
	}
}
