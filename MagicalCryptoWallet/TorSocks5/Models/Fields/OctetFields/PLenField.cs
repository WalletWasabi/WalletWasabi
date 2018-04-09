using MagicalCryptoWallet.Bases;
using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.TorSocks5.TorSocks5.Models.Fields.ByteArrayFields;

namespace MagicalCryptoWallet.TorSocks5.Models.Fields.OctetFields
{
	public class PLenField : OctetSerializableBase
	{
		#region PropertiesAndMembers

		public int Value => ByteValue;

		#endregion

		#region ConstructorsAndInitializers

		public PLenField()
		{

		}

		public PLenField(int value)
		{
			ByteValue = (byte)Guard.InRangeAndNotNull(nameof(value), value, 0, 255);
		}

		#endregion

		#region Serialization

		public void FromPasswdField(PasswdField passwd)
		{
			Guard.NotNull(nameof(passwd), passwd);

			ByteValue = (byte)passwd.ToBytes().Length;
		}

		#endregion
	}
}
