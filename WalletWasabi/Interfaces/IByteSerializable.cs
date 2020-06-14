namespace WalletWasabi.Interfaces
{
	public interface IByteSerializable
	{
		byte ToByte();

		void FromByte(byte b);

		string ToHex(bool xhhSyntax);

		void FromHex(string hex);
	}
}
