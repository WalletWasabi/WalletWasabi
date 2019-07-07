using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.TorSocks5.TorSocks5.Models.Fields.ByteArrayFields;

namespace WalletWasabi.TorSocks5.Models.Fields.OctetFields
{
	public class PLenField : OctetSerializableBase
	{
		#region PropertiesAndMembers

		public int Value => ByteValue;

		#endregion PropertiesAndMembers

		#region ConstructorsAndInitializers

		public PLenField()
		{
		}

		public PLenField(int value)
		{
			ByteValue = (byte)Guard.InRangeAndNotNull(nameof(value), value, 0, 255);
		}

		#endregion ConstructorsAndInitializers

		#region Serialization

		public void FromPasswordField(PasswordField password)
		{
			Guard.NotNull(nameof(password), password);

			ByteValue = (byte)password.ToBytes().Length;
		}

		#endregion Serialization
	}
}
