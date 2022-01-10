using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

public class AuthStatusField : OctetSerializableBase
{
	public static AuthStatusField Success = new(0x00);

	public AuthStatusField(byte value)
	{
		ByteValue = value;
	}

	public bool IsSuccess() => ByteValue == Success.ByteValue;
}
