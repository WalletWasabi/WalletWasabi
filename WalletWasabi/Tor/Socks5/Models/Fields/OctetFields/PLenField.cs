using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;
using WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;

namespace WalletWasabi.Tor.Socks5.Models.Fields.OctetFields
{
	public class PLenField : OctetSerializableBase
	{
		#region Constructors

		public PLenField()
		{
		}

		public PLenField(int value)
		{
			ByteValue = (byte)Guard.InRangeAndNotNull(nameof(value), value, 0, 255);
		}

		#endregion Constructors

		#region PropertiesAndMembers

		public int Value => ByteValue;

		#endregion PropertiesAndMembers

		#region Serialization

		public void FromPasswdField(PasswdField passwd)
		{
			Guard.NotNull(nameof(passwd), passwd);

			ByteValue = (byte)passwd.ToBytes().Length;
		}

		#endregion Serialization
	}
}
