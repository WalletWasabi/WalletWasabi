using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

public class CmdField : OctetSerializableBase
{
	// https://gitweb.torproject.org/torspec.git/tree/socks-extensions.txt
	// The BIND command is not supported.
	// The (SOCKS5) "UDP ASSOCIATE" command is not supported.

	public static readonly CmdField Connect = new(0x01);

	// https://gitweb.torproject.org/torspec.git/tree/socks-extensions.txt#n46
	// As an extension to SOCKS4A and SOCKS5, Tor implements a new command value,
	// "RESOLVE" [F0]. When Tor receives a "RESOLVE" SOCKS command, it initiates
	// a remote lookup of the hostname provided as the target address in the SOCKS
	// request.The reply is either an error(if the address could not be
	// resolved) or a success response.In the case of success, the address is
	// stored in the portion of the SOCKS response reserved for remote IP address.
	public static readonly CmdField Resolve = new(0xF0);

	// https://gitweb.torproject.org/torspec.git/tree/socks-extensions.txt#n55
	// For SOCKS5 only, we support reverse resolution with a new command value,
	// "RESOLVE_PTR" [F1]. In response to a "RESOLVE_PTR" SOCKS5 command with
	// an IPv4 address as its target, Tor attempts to find the canonical
	// hostname for that IPv4 record, and returns it in the "server bound
	// address" portion of the reply.
	public static readonly CmdField ResolvePtr = new(0xF1);

	public CmdField(byte byteValue)
	{
		ByteValue = byteValue;
	}
}
