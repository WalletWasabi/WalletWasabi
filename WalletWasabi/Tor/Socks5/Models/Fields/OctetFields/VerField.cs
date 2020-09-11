using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.OctetFields
{
	public class VerField : OctetSerializableBase
	{
		#region Constructors

		public VerField()
		{
		}

		public VerField(int value)
		{
			ByteValue = (byte)Guard.InRangeAndNotNull(nameof(value), value, 0, 255);
		}

		#endregion Constructors

		#region Statics

		public static VerField Socks5 => new VerField(5);

		#endregion Statics

		#region PropertiesAndMembers

		public int Value => ByteValue;

		#endregion PropertiesAndMembers
	}
}
