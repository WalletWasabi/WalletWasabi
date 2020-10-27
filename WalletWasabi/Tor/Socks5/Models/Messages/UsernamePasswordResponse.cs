using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor.Socks5.Models.Messages
{
	public class UsernamePasswordResponse : ByteArraySerializableBase
	{
		public UsernamePasswordResponse()
		{
		}

		public AuthVerField Ver { get; set; }

		public AuthStatusField Status { get; set; }

		public override void FromBytes(byte[] bytes)
		{
			Guard.NotNullOrEmpty(nameof(bytes), bytes);
			Guard.Same($"{nameof(bytes)}.{nameof(bytes.Length)}", 2, bytes.Length);

			Ver = new AuthVerField(bytes[0]);

			Status = new AuthStatusField(bytes[1]);
		}

		public override byte[] ToBytes() => new byte[]
			{
				Ver.ToByte(),
				Status.ToByte()
			};
	}
}
