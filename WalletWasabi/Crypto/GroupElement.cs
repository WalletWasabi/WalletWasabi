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
				Ge = groupElement;
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

		private GE Ge { get; }

		public bool IsInfinity => Ge.IsInfinity;

		public override bool Equals(object obj) => Equals(obj as GroupElement);

		public bool Equals(GroupElement other) => this == other;

		public override int GetHashCode() => Ge.GetHashCode();

		public static bool operator ==(GroupElement a, GroupElement b)
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

		public static bool operator !=(GroupElement a, GroupElement b) => !(a == b);

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
		{
			Guard.NotNull(nameof(a), a);
			Guard.NotNull(nameof(b), b);

			return new GroupElement(a.Ge.ToGroupElementJacobian().AddVariable(b.Ge, out _));
		}

		public static GroupElement operator -(GroupElement a, GroupElement b)
		{
			Guard.NotNull(nameof(a), a);
			Guard.NotNull(nameof(b), b);

			return a + new GroupElement(b.Ge.Negate());
		}

		/// <param name="scalar">It's ok for the scalar to overflow.</param>
		public static GroupElement operator *(Scalar scalar, GroupElement groupElement)
		{
			Guard.NotNull(nameof(GroupElement), groupElement);

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

		public byte[] ToBytes() => ByteHelpers.Combine(Ge.x.ToBytes(), Ge.y.ToBytes());

		public static GroupElement FromBytes(byte[] bytes)
		{
			Guard.Same($"{nameof(bytes)}.{nameof(bytes.Length)}", 64, bytes.Length);

			// Only infinity can have zeros.
			// If one defines infinity in the constructor, but with non-zero coordinates it'll zero them out.
			// If one defines zero coordinates but not infinity, the constructor will throw invalid variable error.
			if (bytes.All(b => b == 0))
			{
				return Infinity;
			}

			var x = bytes.Take(32).ToArray();
			var y = bytes.Skip(32).ToArray();

			return new GroupElement(new GE(new FE(x), new FE(y)));
		}
	}
}
