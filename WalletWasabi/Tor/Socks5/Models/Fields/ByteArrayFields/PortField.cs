using System.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;

public class PortField : ByteArraySerializableBase
{
	public PortField(byte[] bytes)
	{
		Bytes = Guard.NotNullOrEmpty(nameof(bytes), bytes);
	}

	public PortField(int dstPort)
	{
		Guard.MinimumAndNotNull(nameof(dstPort), dstPort, 0);

		var bytes = BitConverter.GetBytes(dstPort);
		if (bytes[2] != 0 || bytes[3] != 0)
		{
			throw new FormatException($"{nameof(dstPort)} cannot be encoded in two octets. Value: {dstPort}.");
		}

		// https://www.ietf.org/rfc/rfc1928.txt
		// DST.PORT desired destination port in network octet order
		Bytes = bytes.Take(2).Reverse().ToArray();
	}

	private byte[] Bytes { get; }

	public int DstPort => BitConverter.ToInt16(Bytes.Reverse().ToArray(), 0);

	public override byte[] ToBytes() => Bytes;

	public override string ToString() => DstPort.ToString();
}
