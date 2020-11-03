using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields
{
	public class PasswdField : ByteArraySerializableBase
	{
		public PasswdField(byte[] bytes)
		{
			Bytes = Guard.NotNullOrEmpty(nameof(bytes), bytes);
		}

		public PasswdField(string passwd)
			: this(Encoding.UTF8.GetBytes(passwd))
		{
		}

		private byte[] Bytes { get; }

		public string Passwd => Encoding.UTF8.GetString(Bytes); // Tor accepts UTF8 encoded passwd

		public override byte[] ToBytes() => Bytes;
	}
}
