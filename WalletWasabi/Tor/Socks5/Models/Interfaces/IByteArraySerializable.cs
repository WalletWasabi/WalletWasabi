namespace WalletWasabi.Tor.Socks5.Models.Interfaces;

public interface IByteArraySerializable
{
	byte[] ToBytes();

	string ToHex(bool xhhSyntax);
}
