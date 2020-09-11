using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;
using WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;

namespace WalletWasabi.Tor.Socks5.Models.Fields.OctetFields
{
	public class NMethodsField : OctetSerializableBase
	{
		#region Constructors

		public NMethodsField()
		{
		}

		public NMethodsField(int value)
		{
			ByteValue = (byte)Guard.InRangeAndNotNull(nameof(value), value, 0, 255);
		}

		#endregion Constructors

		#region PropertiesAndMembers

		public int Value => ByteValue;

		#endregion PropertiesAndMembers

		#region Serialization

		public void FromMethodsField(MethodsField methods)
		{
			Guard.NotNull(nameof(methods), methods);

			ByteValue = (byte)methods.ToBytes().Length;
		}

		#endregion Serialization
	}
}
