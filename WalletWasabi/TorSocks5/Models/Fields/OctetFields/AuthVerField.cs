using WalletWasabi.Bases;
using WalletWasabi.Helpers;

namespace WalletWasabi.TorSocks5.Models.Fields.OctetFields
{
	public class AuthVerField : OctetSerializableBase
	{
		#region Constructors

		public AuthVerField()
		{
		}

		public AuthVerField(int value)
		{
			ByteValue = (byte)Guard.InRangeAndNotNull(nameof(value), value, 0, 255);
		}

		#endregion Constructors

		#region Statics

		public static AuthVerField Version1 => new AuthVerField(1);

		#endregion Statics

		#region PropertiesAndMembers

		public int Value => ByteValue;

		#endregion PropertiesAndMembers
	}
}
