using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;

public class UNameField : ByteArraySerializableBase
{
	public UNameField(byte[] bytes)
	{
		Bytes = Guard.NotNullOrEmpty(nameof(bytes), bytes);
	}

	public UNameField(string uName)
		: this(Encoding.UTF8.GetBytes(uName))
	{
	}

	private byte[] Bytes { get; }

	public override byte[] ToBytes() => Bytes;
}
