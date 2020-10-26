using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.OctetFields
{
	public class AuthStatusField : OctetSerializableBase
	{
		public AuthStatusField(byte value)
		{
			ByteValue = value;
		}

		public AuthStatusField(int value)
		{
			ByteValue = (byte)Guard.InRangeAndNotNull(nameof(value), value, 0, 255);
		}

		public static AuthStatusField Success => new AuthStatusField(0);

		public int Value => ByteValue;

		public bool IsSuccess() => Value == 0;
	}
}
