using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.TorSocks5.Models.Fields.OctetFields;

namespace WalletWasabi.TorSocks5.Models.Messages
{
	public class MethodSelectionResponse : ByteArraySerializableBase
	{
		#region PropertiesAndMembers

		public VerField Ver { get; set; }

		public MethodField Method { get; set; }

		#endregion PropertiesAndMembers

		#region ConstructorsAndInitializers

		public MethodSelectionResponse()
		{
		}

		public MethodSelectionResponse(MethodField method)
		{
			Method = Guard.NotNull(nameof(method), method);
			Ver = VerField.Socks5;
		}

		#endregion ConstructorsAndInitializers

		#region Serialization

		public override void FromBytes(byte[] bytes)
		{
			Guard.NotNullOrEmpty(nameof(bytes), bytes);
			Guard.Same($"{nameof(bytes)}.{nameof(bytes.Length)}", 2, bytes.Length);

			Ver = new VerField();
			Ver.FromByte(bytes[0]);

			Method = new MethodField();
			Method.FromByte(bytes[1]);
		}

		public override byte[] ToBytes() => new byte[] { Ver.ToByte(), Method.ToByte() };

		#endregion Serialization
	}
}
