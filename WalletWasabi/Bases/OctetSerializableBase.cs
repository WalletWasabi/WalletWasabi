using System;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;

namespace WalletWasabi.Bases
{
	public abstract class OctetSerializableBase : IByteSerializable, IEquatable<OctetSerializableBase>, IEquatable<byte>
	{
		#region PropertiesAndMembers

		protected byte ByteValue { get; set; }

		#endregion PropertiesAndMembers

		#region ConstructorsAndInitializers

		protected OctetSerializableBase()
		{
		}

		#endregion ConstructorsAndInitializers

		#region Serialization

		public byte ToByte() => ByteValue;

		public void FromByte(byte b) => ByteValue = b;

		public string ToHex(bool xhhSyntax = false)
		{
			if (xhhSyntax)
			{
				return $"X'{ByteHelpers.ToHex(ToByte())}'";
			}
			return ByteHelpers.ToHex(ToByte());
		}

		public void FromHex(string hex)
		{
			hex = Guard.NotNullOrEmptyOrWhitespace(nameof(hex), hex, true);

			byte[] bytes = ByteHelpers.FromHex(hex);
			if (bytes.Length != 1)
			{
				throw new FormatException($"{nameof(hex)} must be exactly one byte. Actual: {bytes.Length} bytes. Value: {hex}.");
			}

			ByteValue = bytes[0];
		}

		public override string ToString()
		{
			return ToHex(xhhSyntax: true);
		}

		#endregion Serialization

		#region EqualityAndComparison

		public override bool Equals(object obj) => obj is OctetSerializableBase octetSerializableBase && this == octetSerializableBase;

		public bool Equals(OctetSerializableBase other) => this == other;

		public override int GetHashCode() => ByteValue;

		public static bool operator ==(OctetSerializableBase x, OctetSerializableBase y) => x?.ByteValue == y?.ByteValue;

		public static bool operator !=(OctetSerializableBase x, OctetSerializableBase y) => !(x == y);

		public bool Equals(byte other) => ByteValue == other;

		public static bool operator ==(byte x, OctetSerializableBase y) => x == y?.ByteValue;

		public static bool operator ==(OctetSerializableBase x, byte y) => x?.ByteValue == y;

		public static bool operator !=(byte x, OctetSerializableBase y) => !(x == y);

		public static bool operator !=(OctetSerializableBase x, byte y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
