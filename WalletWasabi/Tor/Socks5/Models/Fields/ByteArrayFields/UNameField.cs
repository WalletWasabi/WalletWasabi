using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields
{
	public class UNameField : ByteArraySerializableBase
	{
		#region Constructors

		public UNameField(byte[] bytes)
		{
			Bytes = Guard.NotNullOrEmpty(nameof(bytes), bytes);
		}

		public UNameField(string uName)
			: this(Encoding.UTF8.GetBytes(uName))
		{
		}

		#endregion Constructors

		#region PropertiesAndMembers

		private byte[] Bytes { get; }

		public string UName => Encoding.UTF8.GetString(Bytes); // Tor accepts UTF8 encoded passwd

		#endregion PropertiesAndMembers

		#region Serialization

		public override byte[] ToBytes() => Bytes;

		#endregion Serialization
	}
}
