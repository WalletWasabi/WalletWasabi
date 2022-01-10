using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

public class AuthVerField : OctetSerializableBase
{
	public static AuthVerField Version1 = new(0x01);

	public AuthVerField(byte value)
	{
		ByteValue = value;
	}

	public int Value => ByteValue;
}
