using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields
{
	public class UNameField : ByteArraySerializableBase
	{
		#region Constructors

		public UNameField()
		{
		}

		public UNameField(string uName)
		{
			Guard.NotNullOrEmpty(nameof(uName), uName);
			Bytes = Encoding.UTF8.GetBytes(uName);
		}

		#endregion Constructors

		#region PropertiesAndMembers

		private byte[] Bytes { get; set; }

		public string UName => Encoding.UTF8.GetString(Bytes); // Tor accepts UTF8 encoded passwd

		#endregion PropertiesAndMembers

		#region Serialization

		public override void FromBytes(byte[] bytes) => Bytes = Guard.NotNullOrEmpty(nameof(bytes), bytes);

		public override byte[] ToBytes() => Bytes;

		#endregion Serialization
	}
}
