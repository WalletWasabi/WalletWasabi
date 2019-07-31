using WalletWasabi.Bases;

namespace WalletWasabi.TorSocks5.TorSocks5.Models.Fields.ByteArrayFields
{
	public class RsvField : OctetSerializableBase
	{
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

		#region ConstructorsAndInitializers

		public RsvField()
		{
		}

		#endregion ConstructorsAndInitializers
	}
}
