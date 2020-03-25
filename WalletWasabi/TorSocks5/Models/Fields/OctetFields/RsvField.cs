using WalletWasabi.Bases;

namespace WalletWasabi.TorSocks5.TorSocks5.Models.Fields.ByteArrayFields
{
	public class RsvField : OctetSerializableBase
	{
		#region Constructors

		public RsvField()
		{
		}

		#endregion Constructors

		#region Statics

		public static RsvField X00
		{
			get
			{
				var rsv = new RsvField();
				rsv.FromHex("00");
				return rsv;
			}
		}

		#endregion Statics
	}
}
