using System;
using System.Collections.Generic;
using System.Text;

namespace MagicalCryptoWallet.Models
{
	public struct Height : IEquatable<Height>, IEquatable<int>, IComparable<Height>, IComparable<int>
	{
		public HeightType Type { get; }
		private readonly int _value;
		public int Value
		{
			get
			{
				if (Type == HeightType.Chain)
					return _value;
				if (Type == HeightType.MemPool)
					return int.MaxValue - 1;
				//if(Type == HeightType.Unknown)
				return int.MaxValue;
			}
		}

		public static Height MemPool => new Height(HeightType.MemPool);
		public static Height Unknown => new Height(HeightType.Unknown);

		public Height(int height)
		{
			if (height < 0) throw new ArgumentException($"{nameof(height)} : {height} cannot be < 0");
			if (height == int.MaxValue) Type = HeightType.Unknown;
			else if (height == int.MaxValue - 1) Type = HeightType.MemPool;
			else Type = HeightType.Chain;
			_value = height;
		}

		public Height(string heightOrHeightType)
		{
			var trimmed = heightOrHeightType.Trim();
			if (trimmed == HeightType.MemPool.ToString())
				this = MemPool;
			else if (trimmed == HeightType.Unknown.ToString())
				this = Unknown;
			else this = new Height(int.Parse(trimmed));
		}

		public Height(HeightType type)
		{
			if (type == HeightType.Chain) throw new NotSupportedException($"For {type} height must be specified");
			Type = type;
			if (Type == HeightType.MemPool)
				_value = int.MaxValue - 1;
			else _value = int.MaxValue; // HeightType.Unknown
		}

		public override string ToString()
		{
			if (Type == HeightType.Chain) return Value.ToString();
			else return Type.ToString();
		}

		#region EqualityAndComparison

		public override bool Equals(object obj) => obj is Height && this == (Height)obj;
		public bool Equals(Height other) => this == other;
		public override int GetHashCode() => Value.GetHashCode();
		public static bool operator ==(Height x, Height y) => x.Value == y.Value;
		public static bool operator !=(Height x, Height y) => !(x == y);

		public bool Equals(int other) => Value == other;
		public static bool operator ==(int x, Height y) => x == y.Value;
		public static bool operator ==(Height x, int y) => x.Value == y;
		public static bool operator !=(int x, Height y) => !(x == y);
		public static bool operator !=(Height x, int y) => !(x == y);

		public int CompareTo(Height other) => Value.CompareTo(other.Value);
		public int CompareTo(int other)
		{
			if (Value > other) return -1;
			if (Value == other) return 0;
			return 1;
		}

		public static bool operator >(Height x, Height y) => x.Value > y.Value;
		public static bool operator <(Height x, Height y) => x.Value < y.Value;
		public static bool operator >=(Height x, Height y) => x.Value >= y.Value;
		public static bool operator <=(Height x, Height y) => x.Value <= y.Value;

		public static bool operator >(int x, Height y) => x > y.Value;
		public static bool operator >(Height x, int y) => x.Value > y;
		public static bool operator <(int x, Height y) => x < y.Value;
		public static bool operator <(Height x, int y) => x.Value < y;
		public static bool operator >=(int x, Height y) => x >= y.Value;
		public static bool operator <=(int x, Height y) => x <= y.Value;
		public static bool operator >=(Height x, int y) => x.Value >= y;
		public static bool operator <=(Height x, int y) => x.Value <= y;

		#endregion
	}

	public enum HeightType
	{
		Chain,
		MemPool,
		Unknown
	}
}
