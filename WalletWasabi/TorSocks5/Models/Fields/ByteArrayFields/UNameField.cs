using System.Text;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;

namespace WalletWasabi.TorSocks5.TorSocks5.Models.Fields.ByteArrayFields
{
	public class UsernameField : ByteArraySerializableBase
	{
		#region PropertiesAndMembers

		private byte[] Bytes { get; set; }

		public string Username => Encoding.UTF8.GetString(Bytes); // Tor accepts UTF8 encoded password

		#endregion PropertiesAndMembers

		#region ConstructorsAndInitializers

		public UsernameField()
		{
		}

		public UsernameField(string username)
		{
			Guard.NotNullOrEmpty(nameof(username), username);
			Bytes = Encoding.UTF8.GetBytes(username);
		}

		#endregion ConstructorsAndInitializers

		#region Serialization

		public override void FromBytes(byte[] bytes) => Bytes = Guard.NotNullOrEmpty(nameof(bytes), bytes);

		public override byte[] ToBytes() => Bytes;

		#endregion Serialization
	}
}
