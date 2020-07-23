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

		private GE Ge { get; }

		public bool IsInfinity => Ge.IsInfinity;

		public override bool Equals(object obj) => Equals(obj as GroupElement);

		public bool Equals(GroupElement other) => this == other;

		public override int GetHashCode() => Ge.GetHashCode();

		public static bool operator ==(GroupElement x, GroupElement y)
		{
			if (x is null && y is null)
			{
				return true;
			}
			else if (x is null || y is null)
			{
				return false;
			}
			else if (x.IsInfinity && y.IsInfinity)
			{
				return true;
			}
			else
			{
				return x.IsInfinity == y.IsInfinity && x.Ge.x == y.Ge.x && x.Ge.y == y.Ge.y;
			}
		}

		public static bool operator !=(GroupElement x, GroupElement y) => !(x == y);
	}
}
