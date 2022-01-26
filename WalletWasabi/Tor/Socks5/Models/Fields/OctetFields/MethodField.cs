using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

public class MethodField : OctetSerializableBase
{
	// https://gitweb.torproject.org/torspec.git/tree/socks-extensions.txt
	// The "NO AUTHENTICATION REQUIRED" (SOCKS5) authentication method[00] is
	// supported; and as of Tor 0.2.3.2-alpha, the "USERNAME/PASSWORD" (SOCKS5)
	// authentication method[02] is supported too, and used as a method to
	// implement stream isolation.As an extension to support some broken clients,
	// we allow clients to pass "USERNAME/PASSWORD" authentication to us even if
	// no authentication was selected.

	public static readonly MethodField NoAuthenticationRequired = new(0x00);

	public static readonly MethodField UsernamePassword = new(0x02);

	public static readonly MethodField NoAcceptableMethods = new(0xFF);

	public MethodField(byte value)
	{
		ByteValue = value;
	}
}
