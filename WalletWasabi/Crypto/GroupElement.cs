using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto
{
	public class GroupElement : IEquatable<GroupElement>
	{
		public GroupElement(GE groupElement)
		{
			if (groupElement.IsInfinity)
			{
				Ge = GE.Infinity;
			}
			else
			{
				Guard.True($"{nameof(groupElement)}.{nameof(groupElement.IsValidVariable)}", groupElement.IsValidVariable);
				Ge = new GE(groupElement.x.Normalize(), groupElement.y.Normalize());
			}
		}

		public GroupElement(GEJ groupElement)
			: this(groupElement.ToGroupElement())
		{
		}

		public static GroupElement Infinity { get; } = new GroupElement(GE.Infinity);

		/// <summary>
		/// The base point defined in the secp256k1 standard used in ECDSA public key derivation.
		/// </summary>
		public static GroupElement G { get; } = new GroupElement(EC.G);

		/// <summary>
		/// Generators for MAC and Show.
		/// </summary>
		public static GroupElement Gw { get; } = FromText("Gw");

		/// <summary>
		/// Generators for MAC and Show.
		/// </summary>
		public static GroupElement Gwp { get; } = FromText("Gwp");

		/// <summary>
		/// Generators for MAC and Show.
		/// </summary>
		public static GroupElement Gx0 { get; } = FromText("Gx0");

		/// <summary>
		/// Generators for MAC and Show.
		/// </summary>
		public static GroupElement Gx1 { get; } = FromText("Gx1");

		/// <summary>
		/// Generators for MAC and Show.
		/// </summary>
		public static GroupElement GV { get; } = FromText("GV");

		/// <summary>
		/// Generators for Pedersen commitments.
		/// </summary>
		public static GroupElement Gg { get; } = FromText("Gg");

		/// <summary>
		/// Generators for Pedersen commitments.
		/// </summary>
		public static GroupElement Gh { get; } = FromText("Gh");

		/// <summary>
		/// Generator for attributes M_{ai}.
		/// </summary>
		public static GroupElement Ga { get; } = FromText("Ga");

		/// <summary>
		/// Generator for serial numbers.
		/// </summary>
		public static GroupElement Gs { get; } = FromText("Gs");

		private GE Ge { get; }

		public bool IsInfinity => Ge.IsInfinity;

		public override bool Equals(object? obj) => Equals(obj as GroupElement);

		public bool Equals(GroupElement? other) => this == other;

		public override int GetHashCode() => Ge.GetHashCode();

		public static bool operator ==(GroupElement? a, GroupElement? b)
		{
			if (a is null && b is null)
			{
				return true;
			}
			else if (a is null || b is null)
			{
				return false;
			}
			else if (a.IsInfinity && b.IsInfinity)
			{
				return true;
			}
			else
			{
				return a.IsInfinity == b.IsInfinity && a.Ge.x == b.Ge.x && a.Ge.y == b.Ge.y;
			}
		}

		public static bool operator !=(GroupElement? a, GroupElement? b) => !(a == b);

		/// <summary>
		/// ToString is only used for nice visual representation during debugging. Do not rely on the result for anything else.
		/// </summary>
		public override string ToString()
		{
			if (IsInfinity)
			{
				return "Infinity";
			}
			else if (Ge.x == EC.G.x && Ge.y == EC.G.y)
			{
				return $"Standard Generator, {Ge.x.ToC("x")}{Ge.y.ToC("y")}";
			}
			else
			{
				return $"{Ge.x.ToC("x")}{Ge.y.ToC("y")}";
			}
		}

		public static GroupElement operator +(GroupElement a, GroupElement b)
			=> new GroupElement(a.Ge.ToGroupElementJacobian().AddVariable(b.Ge, out _));

		public static GroupElement operator -(GroupElement a, GroupElement b)
			=> a + new GroupElement(b.Ge.Negate());

		/// <param name="scalar">It's ok for the scalar to overflow.</param>
		public static GroupElement operator *(Scalar scalar, GroupElement groupElement)
		{
			// For some strange reason scalar * GE.Infinity isn't infinity. Let's fix it as it should be, since:
			// 2 * GE.Infinity = GE.Infinity + GE.Infinity = GE.Infinity.
			if (groupElement.IsInfinity)
			{
				return Infinity;
			}

			return new GroupElement(scalar * groupElement.Ge);
		}

		/// <param name="scalar">It's ok for the scalar to overflow.</param>
		public static GroupElement operator *(GroupElement groupElement, Scalar scalar) => scalar * groupElement;

		public GroupElement Negate() => new GroupElement(Ge.Negate());

		public byte[] ToBytes()
		{
			// Buffer to store the serialized Group Element in its **compressed** format.
			// It requires 32 bytes for the 256bits `x` coordinate and an extra byte for
			// the EVEN/ODD flag.
			const int CompressedLength = 32 + 1;
			Span<byte> buffer = new byte[CompressedLength];

			var x = Ge.x;
			var y = Ge.y;

			buffer[0] = (Ge.IsInfinity, y.IsOdd) switch
			{
				(true, _) => 0, // see http://www.secg.org/sec1-v2.pdf sections 2.3.3-4:
				(false, true) => GE.SECP256K1_TAG_PUBKEY_ODD,
				(false, false) => GE.SECP256K1_TAG_PUBKEY_EVEN,
			};
			x.WriteToSpan(buffer[1..]);
			return buffer.ToArray();
		}

		public static GroupElement FromBytes(byte[] bytes)
		{
			const int CompressedLength = 32 + 1;
			Guard.Same($"{nameof(bytes)}.{nameof(bytes.Length)}", CompressedLength, bytes.Length);

			static GroupElement Parse(Span<byte> buffer, bool isOdd) =>
				FE.TryCreate(buffer, out var x) && GE.TryCreateXOVariable(x, isOdd, out var ge)
				? new GroupElement(ge)
				: throw new ArgumentException("Argument is not a valid group element.", nameof(bytes));

			return bytes[0] switch
			{
				0 => Infinity,
				GE.SECP256K1_TAG_PUBKEY_ODD => Parse(bytes[1..], isOdd: true),
				GE.SECP256K1_TAG_PUBKEY_EVEN => Parse(bytes[1..], isOdd: false),
				_ => throw new ArgumentException($"Argument is not a well-formatted group element.", nameof(bytes))
			};
		}

		/// <summary>
		/// Deterministically creates a group element from the given text.
		/// Uniqueness relies on the SHA256 hash function.
		/// </summary>
		public static GroupElement FromText(string text)
		{
			Guard.NotNull(nameof(text), text);

			FE x;
			GE ge;
			int nonce = 0;
			using var sha256 = System.Security.Cryptography.SHA256.Create();
			do
			{
				x = new FE(sha256.ComputeHash(Encoding.UTF8.GetBytes(text + nonce)));
				nonce++;
			}
			while (!GE.TryCreateXOVariable(x, true, out ge));

			return new GroupElement(ge);
		}
	}
}
