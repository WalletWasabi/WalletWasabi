using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.OctetFields
{
	public class RsvField : OctetSerializableBase
	{
		public RsvField(byte byteValue)
		{
			ByteValue = byteValue;
		}

		public static RsvField X00 => new RsvField(0x00);
	}
}
