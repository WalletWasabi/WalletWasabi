using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Interfaces;

namespace WalletWasabi.Tor.Socks5.Models.Bases;

public abstract class ByteArraySerializableBase : IByteArraySerializable, IEquatable<ByteArraySerializableBase>, IEquatable<byte[]>
{
	public abstract byte[] ToBytes();

	public string ToHex(bool xhhSyntax = false)
	{
		if (xhhSyntax)
		{
			return $"X'{ByteHelpers.ToHex(ToBytes())}'";
		}
		return ByteHelpers.ToHex(ToBytes());
	}

	public override string ToString()
	{
		return ToHex(xhhSyntax: true);
	}

	public static bool operator ==(ByteArraySerializableBase? x, ByteArraySerializableBase? y) => ByteHelpers.CompareFastUnsafe(x?.ToBytes(), y?.ToBytes());

	public static bool operator !=(ByteArraySerializableBase? x, ByteArraySerializableBase? y) => !(x == y);

	public static bool operator ==(byte[]? x, ByteArraySerializableBase? y) => ByteHelpers.CompareFastUnsafe(x, y?.ToBytes());

	public static bool operator ==(ByteArraySerializableBase? x, byte[]? y) => ByteHelpers.CompareFastUnsafe(x?.ToBytes(), y);

	public static bool operator !=(byte[]? x, ByteArraySerializableBase? y) => !(x == y);

	public static bool operator !=(ByteArraySerializableBase? x, byte[]? y) => !(x == y);

	public override bool Equals(object? obj) => Equals(obj as ByteArraySerializableBase);

	public bool Equals(ByteArraySerializableBase? other) => this == other;

	public override int GetHashCode()
	{
		// https://github.com/bcgit/bc-csharp/blob/b19e68a517e56ef08cd2e50df4dcb8a96ddbe507/crypto/src/util/Arrays.cs#L206
		var bytes = ToBytes();
		if (bytes is null)
		{
			return 0;
		}

		int i = bytes.Length;
		int hash = i + 1;

		while (--i >= 0)
		{
			hash *= 257;
			hash ^= bytes[i];
		}

		return hash;
	}

	public bool Equals(byte[]? other) => ByteHelpers.CompareFastUnsafe(ToBytes(), other);
}
