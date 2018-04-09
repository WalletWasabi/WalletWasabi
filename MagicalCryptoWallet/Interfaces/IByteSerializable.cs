namespace MagicalCryptoWallet.Interfaces
{
	public interface IByteSerializable
    {
		byte ToByte();
		void FromByte(byte b);
		string ToHex();
		string ToHex(bool xhhSyntax);
		void FromHex(string hex);
	}
}
