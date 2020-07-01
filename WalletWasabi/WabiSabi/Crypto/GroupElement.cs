using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.WabiSabi.Crypto
{
	public class GroupElement : IEquatable<GroupElement>
	{
		public GroupElement(GE secpGroupElement)
		{
			if (secpGroupElement.IsInfinity)
			{
				if (secpGroupElement.x != FE.Zero || secpGroupElement.y != FE.Zero)
				{
					throw new ArgumentException("Infinity can only be initialized with zero group elements.");
				}
			}
			SecpGroupElement = secpGroupElement;
		}

		public GroupElement(GEJ jacobianSecpGroupElement)
			: this(jacobianSecpGroupElement.ToGroupElement())
		{
		}

		public static GroupElement Infinity => new GroupElement(GE.Infinity);
		public static GroupElement Zero => new GroupElement(GE.Zero);

		public GE SecpGroupElement { get; }
		public bool IsInfinity => SecpGroupElement.IsInfinity;

		public GroupElement Negate() => new GroupElement(SecpGroupElement.Negate());

		public override bool Equals(object obj) => Equals(obj as GroupElement);

		public bool Equals(GroupElement other) => this == other;

		public override int GetHashCode() => SecpGroupElement.GetHashCode();

		public static bool operator ==(GroupElement x, GroupElement y)
		{
			if (ReferenceEquals(x, y))
			{
				return true;
			}
			else if (x is null && y is null)
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
			else if (x.IsInfinity || y.IsInfinity)
			{
				return false;
			}
			else
			{
				// ToDo: Explanation here.
				var yNegate = y.Negate();
				var xyNegateSum = x + yNegate;
				return xyNegateSum.IsInfinity;
			}
		}

		public static bool operator !=(GroupElement x, GroupElement y) => !(x == y);

		public static GroupElement operator +(GroupElement x, GroupElement y)
			=> new GroupElement(x.SecpGroupElement.ToGroupElementJacobian().AddVariable(y.SecpGroupElement, out _));
	}
}
