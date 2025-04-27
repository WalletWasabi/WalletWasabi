using System.Linq;
using System.Text;
using NBitcoin;
using static NBitcoin.Crypto.Hashes;

namespace WalletWasabi.Crypto;

public class Slip21Node
{
	private static readonly int KEY_SIZE = 32;
	private byte[] _data;

	public Slip21Node(byte[] data)
	{
		_data = data.Length == 64
			? data
			: throw new ArgumentException("The data array has to be 64 bytes long.", nameof(data));
	}

	public Key Key => new(_data[KEY_SIZE..]);

	public static Slip21Node FromSeed(byte[] seed) =>
		new(HMACSHA512(Encoding.ASCII.GetBytes("Symmetric key seed"), seed));

	public Slip21Node DeriveChild(string label) =>
		DeriveChild(Encoding.ASCII.GetBytes(label));

	public Slip21Node DeriveChild(byte[] label) =>
		new(HMACSHA512(_data[..KEY_SIZE], label.Prepend((byte)0x00).ToArray()));
}
