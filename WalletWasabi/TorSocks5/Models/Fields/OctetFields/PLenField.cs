using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.TorSocks5.TorSocks5.Models.Fields.ByteArrayFields;

namespace WalletWasabi.TorSocks5.Models.Fields.OctetFields
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
