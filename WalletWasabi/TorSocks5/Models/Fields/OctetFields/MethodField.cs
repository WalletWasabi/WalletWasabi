using WalletWasabi.Bases;

namespace WalletWasabi.TorSocks5.Models.Fields.OctetFields
{
	public class MethodField : OctetSerializableBase
	{
		// https://gitweb.torproject.org/torspec.git/tree/socks-extensions.txt
		// The "NO AUTHENTICATION REQUIRED" (SOCKS5) authentication method[00] is
		// supported; and as of Tor 0.2.3.2-alpha, the "USERNAME/PASSWORD" (SOCKS5)
		// authentication method[02] is supported too, and used as a method to
		// implement stream isolation.As an extension to support some broken clients,
		// we allow clients to pass "USERNAME/PASSWORD" authentication to us even if
		// no authentication was selected.

		public static MethodField NoAuthenticationRequired
		{
			get
			{
				var method = new MethodField();
				method.FromHex("00");
				return method;
			}
		}

		public static MethodField UsernamePassword
		{
			get
			{
				var method = new MethodField();
				method.FromHex("02");
				return method;
			}
		}

		public static MethodField NoAcceptableMethods
		{
			get
			{
				var method = new MethodField();
				method.FromHex("FF");
				return method;
			}
		}

		#region ConstructorsAndInitializers

		public MethodField()
		{
		}

		#endregion ConstructorsAndInitializers
	}
}
