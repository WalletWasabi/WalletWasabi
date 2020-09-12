using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.OctetFields
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
