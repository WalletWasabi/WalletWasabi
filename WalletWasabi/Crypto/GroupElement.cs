using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
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
			else if (groupElement.IsGenerator())
			{
				Ge = EC.G;
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
		public static GroupElement Generator { get; } = new GroupElement(EC.G);

		private GE Ge { get; }

		public bool IsInfinity => Ge.IsInfinity;
		public bool IsGenerator => Ge.IsGenerator();

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
			else if (IsGenerator)
			{
				return $"Generator, {Ge.x.ToC("x")}{Ge.y.ToC("y")}";
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

		public GroupElement Negate() => new GroupElement(Ge.Negate());
	}
}
