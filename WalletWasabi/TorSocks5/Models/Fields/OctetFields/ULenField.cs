using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.TorSocks5.TorSocks5.Models.Fields.ByteArrayFields;

namespace WalletWasabi.TorSocks5.Models.Fields.OctetFields
{
	public class ULenField : OctetSerializableBase
	{
		#region PropertiesAndMembers

		public int Value => ByteValue;

		#endregion PropertiesAndMembers

		#region ConstructorsAndInitializers

		public ULenField()
		{
		}

		public ULenField(int value)
		{
			ByteValue = (byte)Guard.InRangeAndNotNull(nameof(value), value, 0, 255);
		}

		#endregion ConstructorsAndInitializers

		#region Serialization

		public void FromUNameField(UNameField uName)
		{
			ByteValue = (byte)uName.ToBytes().Length;
		}

		#endregion Serialization
	}
}
