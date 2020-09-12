namespace WalletWasabi.Tor.Socks5.Models.Interfaces
{
	public interface IByteArraySerializable
	{
		byte[] ToBytes();

		void FromBytes(params byte[] bytes);

		string ToHex(bool xhhSyntax);

		void FromHex(string hex);
	}
}
