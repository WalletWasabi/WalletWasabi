using System.Text;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;

namespace WalletWasabi.TorSocks5.TorSocks5.Models.Fields.ByteArrayFields
{
	public class PasswordField : ByteArraySerializableBase
	{
		#region PropertiesAndMembers

		private byte[] Bytes { get; set; }

		public string Password => Encoding.UTF8.GetString(Bytes); // Tor accepts UTF8 encoded password

		#endregion PropertiesAndMembers

		#region ConstructorsAndInitializers

		public PasswordField()
		{
		}

		public PasswordField(string password)
		{
			Guard.NotNullOrEmpty(nameof(password), password);
			Bytes = Encoding.UTF8.GetBytes(password);
		}

		#endregion ConstructorsAndInitializers

		#region Serialization

		public override void FromBytes(byte[] bytes) => Bytes = Guard.NotNullOrEmpty(nameof(bytes), bytes);

		public override byte[] ToBytes() => Bytes;

		#endregion Serialization
	}
}
