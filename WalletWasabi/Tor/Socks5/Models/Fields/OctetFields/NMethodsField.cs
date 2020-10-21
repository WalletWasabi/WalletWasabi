using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;
using WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;

namespace WalletWasabi.Tor.Socks5.Models.Fields.OctetFields
{
	public class NMethodsField : OctetSerializableBase
	{
		public NMethodsField()
		{
		}

		public int Value => ByteValue;

		public void FromMethodsField(MethodsField methods)
		{
			Guard.NotNull(nameof(methods), methods);

			ByteValue = (byte)methods.ToBytes().Length;
		}
	}
}
