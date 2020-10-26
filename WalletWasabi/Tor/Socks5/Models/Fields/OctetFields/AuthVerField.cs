using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.OctetFields
{
	public class AuthVerField : OctetSerializableBase
	{
		public AuthVerField(byte value)
		{
			ByteValue = value;
		}

		public AuthVerField(int value)
		{
			ByteValue = (byte)Guard.InRangeAndNotNull(nameof(value), value, 0, 255);
		}

		public static AuthVerField Version1 => new AuthVerField(1);

		public int Value => ByteValue;
	}
}
