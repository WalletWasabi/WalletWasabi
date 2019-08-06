using WalletWasabi.Bases;

namespace WalletWasabi.TorSocks5.Models.Fields.OctetFields
{
	public class CmdField : OctetSerializableBase
	{
		#region Statics

		// https://gitweb.torproject.org/torspec.git/tree/socks-extensions.txt
		// The BIND command is not supported.
		// The (SOCKS5) "UDP ASSOCIATE" command is not supported.

		public static CmdField Connect
		{
			get
			{
				var cmd = new CmdField();
				cmd.FromHex("01");
				return cmd;
			}
		}

		// https://gitweb.torproject.org/torspec.git/tree/socks-extensions.txt#n46
		// As an extension to SOCKS4A and SOCKS5, Tor implements a new command value,
		// "RESOLVE" [F0]. When Tor receives a "RESOLVE" SOCKS command, it initiates
		// a remote lookup of the hostname provided as the target address in the SOCKS
		// request.The reply is either an error(if the address could not be
		// resolved) or a success response.In the case of success, the address is
		// stored in the portion of the SOCKS response reserved for remote IP address.
		public static CmdField Resolve
		{
			get
			{
				var cmd = new CmdField();
				cmd.FromHex("F0");
				return cmd;
			}
		}

		// https://gitweb.torproject.org/torspec.git/tree/socks-extensions.txt#n55
		// For SOCKS5 only, we support reverse resolution with a new command value,
		// "RESOLVE_PTR" [F1]. In response to a "RESOLVE_PTR" SOCKS5 command with
		// an IPv4 address as its target, Tor attempts to find the canonical
		// hostname for that IPv4 record, and returns it in the "server bound
		// address" portion of the reply.
		public static CmdField ResolvePtr
		{
			get
			{
				var cmd = new CmdField();
				cmd.FromHex("F1");
				return cmd;
			}
		}

		#endregion Statics

		#region ConstructorsAndInitializers

		public CmdField()
		{
		}

		#endregion ConstructorsAndInitializers
	}
}
