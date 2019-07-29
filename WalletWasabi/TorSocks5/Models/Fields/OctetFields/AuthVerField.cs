using WalletWasabi.Bases;
using WalletWasabi.Helpers;

namespace WalletWasabi.TorSocks5.Models.Fields.OctetFields
{
	public class AuthVerField : OctetSerializableBase
	{
		#region Statics

		public static AuthVerField Version1 => new AuthVerField(1);

		#endregion Statics

		#region PropertiesAndMembers

		public int Value => ByteValue;

		#endregion PropertiesAndMembers

		#region ConstructorsAndInitializers

		public AuthVerField()
		{
		}

		public AuthVerField(int value)
		{
			ByteValue = (byte)Guard.InRangeAndNotNull(nameof(value), value, 0, 255);
		}

		#endregion ConstructorsAndInitializers
	}
}
