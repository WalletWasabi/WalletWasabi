using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor.Socks5.Models.Messages
{
	public class MethodSelectionResponse : ByteArraySerializableBase
	{
		#region Constructors

		public MethodSelectionResponse()
		{
		}

		public MethodSelectionResponse(MethodField method)
		{
			Method = Guard.NotNull(nameof(method), method);
			Ver = VerField.Socks5;
		}

		#endregion Constructors

		#region PropertiesAndMembers

		public VerField Ver { get; set; }

		public MethodField Method { get; set; }

		#endregion PropertiesAndMembers

		#region Serialization

		public override void FromBytes(byte[] bytes)
		{
			Guard.NotNullOrEmpty(nameof(bytes), bytes);
			Guard.Same($"{nameof(bytes)}.{nameof(bytes.Length)}", 2, bytes.Length);

			Ver = new VerField(bytes[0]);

			Method = new MethodField();
			Method.FromByte(bytes[1]);
		}

		public override byte[] ToBytes() => new byte[]
			{
				Ver.ToByte(),
				Method.ToByte()
			};

		#endregion Serialization
	}
}
