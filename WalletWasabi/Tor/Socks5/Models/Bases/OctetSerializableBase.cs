using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Interfaces;

namespace WalletWasabi.Tor.Socks5.Models.Bases;

public abstract class OctetSerializableBase : IByteSerializable, IEquatable<OctetSerializableBase>, IEquatable<byte>
{
	protected byte ByteValue { get; init; }

	#region Serialization

	public byte ToByte() => ByteValue;

	public string ToHex(bool xhhSyntax = false)
	{
		if (xhhSyntax)
		{
			return $"X'{ByteHelpers.ToHex(ToByte())}'";
		}
		return ByteHelpers.ToHex(ToByte());
	}

	public override string ToString()
	{
		return ToHex(xhhSyntax: true);
	}

	#endregion Serialization

	#region EqualityAndComparison

	public static bool operator ==(OctetSerializableBase? x, OctetSerializableBase? y) => x?.ByteValue == y?.ByteValue;

	public static bool operator !=(OctetSerializableBase? x, OctetSerializableBase? y) => !(x == y);

	public static bool operator ==(byte x, OctetSerializableBase? y) => x == y?.ByteValue;

	public static bool operator ==(OctetSerializableBase? x, byte y) => x?.ByteValue == y;

	public static bool operator !=(byte x, OctetSerializableBase? y) => !(x == y);

	public static bool operator !=(OctetSerializableBase? x, byte y) => !(x == y);

	public override bool Equals(object? obj) => Equals(obj as OctetSerializableBase);

	public bool Equals(OctetSerializableBase? other) => this == other;

	public override int GetHashCode() => ByteValue;

	public bool Equals(byte other) => ByteValue == other;

	#endregion EqualityAndComparison
}
