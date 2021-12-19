using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor.Socks5.Models.Messages;

public class UsernamePasswordResponse : ByteArraySerializableBase
{
	/// <param name="bytes">2 bytes are required to be passed in.</param>
	public UsernamePasswordResponse(byte[] bytes)
	{
		Guard.Same($"{nameof(bytes)}.{nameof(bytes.Length)}", 2, bytes.Length);

		Ver = new AuthVerField(bytes[0]);
		Status = new AuthStatusField(bytes[1]);
	}

	public AuthVerField Ver { get; }

	public AuthStatusField Status { get; }

	public override byte[] ToBytes()
		=> new byte[]
		{
				Ver.ToByte(),
				Status.ToByte()
		};
}
