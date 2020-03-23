using WalletWasabi.Bases;
using WalletWasabi.Helpers;

namespace WalletWasabi.TorSocks5.Models.Fields.OctetFields
{
	public class AuthStatusField : OctetSerializableBase
	{
		#region Constructors

		public AuthStatusField()
		{
		}

		public AuthStatusField(int value)
		{
			ByteValue = (byte)Guard.InRangeAndNotNull(nameof(value), value, 0, 255);
		}

		#endregion Constructors

		#region Statics

		public static AuthStatusField Success => new AuthStatusField(0);

		#endregion Statics

		#region PropertiesAndMembers

		public int Value => ByteValue;

		#endregion PropertiesAndMembers

		#region

		public bool IsSuccess() => Value == 0;

		#endregion
	}
}
