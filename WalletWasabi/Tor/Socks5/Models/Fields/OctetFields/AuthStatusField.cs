using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.OctetFields
{
	public class AuthStatusField : OctetSerializableBase
	{
		public AuthStatusField(byte value)
		{
			ByteValue = value;
		}

		public static AuthStatusField Success = new AuthStatusField(0x00);

		public bool IsSuccess() => ByteValue == Success.ByteValue;
	}
}
