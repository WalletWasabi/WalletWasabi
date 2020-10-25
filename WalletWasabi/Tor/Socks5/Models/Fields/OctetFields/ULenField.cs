using WalletWasabi.Tor.Socks5.Models.Bases;
using WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;

namespace WalletWasabi.Tor.Socks5.Models.Fields.OctetFields
{
	public class ULenField : OctetSerializableBase
	{
		public ULenField()
		{
		}

		public int Value => ByteValue;

		public void FromUNameField(UNameField uName)
		{
			ByteValue = (byte)uName.ToBytes().Length;
		}
	}
}
