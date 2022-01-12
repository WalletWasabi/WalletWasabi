namespace WalletWasabi.Tor.Socks5.Models.Interfaces;

public interface IByteSerializable
{
	byte ToByte();

	string ToHex(bool xhhSyntax);
}
