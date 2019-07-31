using System;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models
{
	public struct Height : IEquatable<Height>, IEquatable<int>, IComparable<Height>, IComparable<int>
	{
		public HeightType Type { get; }

		/// <summary>
		/// Gets the height value according to the Height type.
		/// </summary>
		public int Value { get; }

		/// <summary>
		/// Gets a new Height instance for mempool
		/// </summary>
		/// <returns></returns>
		public static Height Mempool { get; } = new Height(HeightType.Mempool);

		/// <summary>
		/// Gets a new Height instance for unknown (no chain, no mempool)
		/// </summary>
		/// <returns></returns>
		public static Height Unknown { get; } = new Height(HeightType.Unknown);

		/// <summary>
		/// Creates and initializes a new Height instance
		/// </summary>
		/// <param name="height">The height value to initialize the instance.
		/// If height value is (Int32.MaxValue -1) then the Height type is set to Mempool.
		/// If height value is (Int32.MaxValue) then the Height tpe is set to Unknown;
		/// Otherwise the Height type is set as Chain.
		/// </param>
		/// <exception href="ArgumentException">When height value is less than zero.</exception>
		public Height(int height)
		{
			if (height < 0)
			{
				throw new ArgumentException($"{nameof(height)} : {height} cannot be less than zero");
			}

			if (height == Unknown.Value)
			{
				Type = HeightType.Unknown;
			}
			else if (height == Mempool.Value)
			{
				Type = HeightType.Mempool;
			}
			else
			{
				Type = HeightType.Chain;
			}

			Value = height;
		}

		/// <param name="heightOrHeightType">The height numerical value as its string representation
		/// or well the strings "Mempool" or "Unknown" for the default initial height of those Heights.
		/// </param>
		public static bool TryParse(string heightOrHeightType, out Height height)
		{
			height = default;

			var correct = Guard.Correct(heightOrHeightType);
			if (correct == "")
			{
				return false;
			}

			if (HeightType.Mempool.ToString().Equals(correct, StringComparison.OrdinalIgnoreCase))
			{
				height = Mempool;
				return true;
			}
			else if (HeightType.Unknown.ToString().Equals(correct, StringComparison.OrdinalIgnoreCase))
			{
				height = Unknown;
				return true;
			}
			else if (int.TryParse(correct, out int h))
			{
				try
				{
					height = new Height(h);
					return true;
				}
				catch
				{
					return false;
				}
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Creates and initializes a new Height instance
		/// </summary>
		/// <param name="type">Height type for the created instance.</param>
		/// <exception href="NotSupportedException">When type is equal to HeightType.Chain.</exception>
		public Height(HeightType type)
		{
			if (type == HeightType.Chain)
			{
				throw new NotSupportedException($"For {type} height must be specified");
			}

			Type = type;

			Value = Type == HeightType.Mempool
				? int.MaxValue - 1
				: int.MaxValue;
		}

		/// <summary>
		/// Implicit conversion from Int32 to Height.
		/// </summary>
		/// <param name="value">Int32 to convert to Height instance.</param>
		public static implicit operator Height(int value) => new Height(value);

		/// <summary>
		/// Implicit conversion from Height to Int32 value.
		/// </summary>
		/// <param name="height">Height value to convert to Int32.</param>
		public static implicit operator int(Height height) => height.Value;

		/// <inheritdoc/>
		public override string ToString()
		{
			if (Type == HeightType.Chain)
			{
				return Value.ToString();
			}

			return Type.ToString();
		}

		#region EqualityAndComparison

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is Height height && this == height;

		/// <inheritdoc/>
		public bool Equals(Height other) => this == other;

		/// <inheritdoc/>
		public override int GetHashCode() => Value.GetHashCode();

		/// <summary>
		/// Performs a comparison and return if side are equal
		/// </summary>
		/// <param name="x">The left-hand Height instance.</param>
		/// <param name="y">The right-hand Height instance.</param>
		/// <returns>true if lhs and rhs are equal; otherwise false.</returns>
		public static bool operator ==(Height x, Height y) => x.Value == y.Value;

		/// <summary>
		/// Performs a comparison and return if side are not equal
		/// </summary>
		/// <param name="x">The left-hand Height instance.</param>
		/// <param name="y">The right-hand Height instance.</param>
		/// <returns>true if lhs and rhs are not equal; otherwise false.</returns>
		public static bool operator !=(Height x, Height y) => !(x == y);

		/// <summary>
		/// Performs a comparison and return if side are equal
		/// </summary>
		/// <param name="other">The value to compare.</param>
		/// <returns>true if this and other are equal; otherwise false.</returns>
		public bool Equals(int other) => Value == other;

		/// <summary>
		/// Performs a comparison and return if side are equal
		/// </summary>
		/// <param name="x">The left-hand Int32 value to compare.</param>
		/// <param name="y">The right-hand Height value to compare.</param>
		/// <returns>true if this and other are equal; otherwise false.</returns>
		public static bool operator ==(int x, Height y) => x == y.Value;

		/// <summary>
		/// Performs a comparison and return if side are equal
		/// </summary>
		/// <param name="x">The left-hand Height value to compare.</param>
		/// <param name="y">The right-hand Int32 value to compare.</param>
		/// <returns>true if this and other are equal; otherwise false.</returns>
		public static bool operator ==(Height x, int y) => x.Value == y;

		/// <summary>
		/// Performs a comparison and return if side are not equal
		/// </summary>
		/// <param name="x">The left-hand Int32 value to compare.</param>
		/// <param name="y">The right-hand Height value to compare.</param>
		/// <returns>true if this and other are not equal; otherwise false.</returns>
		public static bool operator !=(int x, Height y) => !(x == y);

		/// <summary>
		/// Performs a comparison and return if side are not equal
		/// </summary>
		/// <param name="x">The left-hand Height value to compare.</param>
		/// <param name="y">The right-hand Int32 value to compare.</param>
		/// <returns>true if this and other are not equal; otherwise false.</returns>
		public static bool operator !=(Height x, int y) => !(x == y);

		/// <summary>
		/// Performs a comparison and return if compared values are equal, greater or less than the other one
		/// </summary>
		/// <param name="other">The height value to compare against.</param>
		/// <returns>0 if this an other are equal, -1 if this is less than other and 1 if this is greater than other.</returns>
		public int CompareTo(Height other) => Value.CompareTo(other.Value);

		/// <summary>
		/// Performs a comparison and return if compared values are equal, greater or less than the other one
		/// </summary>
		/// <param name="other">The Int32 height value to compare against.</param>
		/// <returns>0 if this an other are equal, -1 if this is less than other and 1 if this is greater than other.</returns>
		public int CompareTo(int other)
		{
			return Value.CompareTo(other);
		}

		/// <summary>
		/// Performs a comparison and return if left-side value is greater than right-side value.
		/// </summary>
		/// <param name="x">The left-hand Height value to compare.</param>
		/// <param name="y">The right-hand Height value to compare.</param>
		/// <returns>true if left-hand value is greater than right-side value; otherwise false.</returns>
		public static bool operator >(Height x, Height y) => x.Value > y.Value;

		/// <summary>
		/// Performs a comparison and return if left-side value is less than right-side value.
		/// </summary>
		/// <param name="x">The left-hand Height value to compare.</param>
		/// <param name="y">The right-hand Height value to compare.</param>
		/// <returns>true if left-hand value is less than right-side value; otherwise false.</returns>
		public static bool operator <(Height x, Height y) => x.Value < y.Value;

		/// <summary>
		/// Performs a comparison and return if left-side value is greater than or equal to right-side value.
		/// </summary>
		/// <param name="x">The left-hand Height value to compare.</param>
		/// <param name="y">The right-hand Height value to compare.</param>
		/// <returns>true if left-hand value is greater than or equal to right-side value; otherwise false.</returns>
		public static bool operator >=(Height x, Height y) => x.Value >= y.Value;

		/// <summary>
		/// Performs a comparison and return if left-side value is less than or equal to right-side value.
		/// </summary>
		/// <param name="x">The left-hand Height value to compare.</param>
		/// <param name="y">The right-hand Height value to compare.</param>
		/// <returns>true if left-hand value is less than or equal to right-side value; otherwise false.</returns>
		public static bool operator <=(Height x, Height y) => x.Value <= y.Value;

		/// <summary>
		/// Performs a comparison and return if left-side value is greater than right-side value.
		/// </summary>
		/// <param name="x">The left-hand Int32 value to compare.</param>
		/// <param name="y">The right-hand Height value to compare.</param>
		/// <returns>true if left-hand value is greater than right-side value; otherwise false.</returns>
		public static bool operator >(int x, Height y) => x > y.Value;

		/// <summary>
		/// Performs a comparison and return if left-side value is greater than right-side value.
		/// </summary>
		/// <param name="x">The left-hand Height  value to compare.</param>
		/// <param name="y">The right-hand Int32 value to compare.</param>
		/// <returns>true if left-hand value is greater than right-side value; otherwise false.</returns>
		public static bool operator >(Height x, int y) => x.Value > y;

		/// <summary>
		/// Performs a comparison and return if left-side value is less than right-side value.
		/// </summary>
		/// <param name="x">The left-hand Int32 value to compare.</param>
		/// <param name="y">The right-hand Height value to compare.</param>
		/// <returns>true if left-hand value is less than right-side value; otherwise false.</returns>
		public static bool operator <(int x, Height y) => x < y.Value;

		/// <summary>
		/// Performs a comparison and return if left-side value is less than right-side value.
		/// </summary>
		/// <param name="x">The left-hand Height value to compare.</param>
		/// <param name="y">The right-hand Int32 value to compare.</param>
		/// <returns>true if left-hand value is less than right-side value; otherwise false.</returns>
		public static bool operator <(Height x, int y) => x.Value < y;

		/// <summary>
		/// Performs a comparison and return if left-side value is greater than or equal to right-side value.
		/// </summary>
		/// <param name="x">The left-hand Int32 value to compare.</param>
		/// <param name="y">The right-hand Height value to compare.</param>
		/// <returns>true if left-hand value is greater than or equal to right-side value; otherwise false.</returns>
		public static bool operator >=(int x, Height y) => x >= y.Value;

		/// <summary>
		/// Performs a comparison and return if left-side value is less than or equal to right-side value.
		/// </summary>
		/// <param name="x">The left-hand Int32 value to compare.</param>
		/// <param name="y">The right-hand Height value to compare.</param>
		/// <returns>true if left-hand value is less than or equal to right-side value; otherwise false.</returns>
		public static bool operator <=(int x, Height y) => x <= y.Value;

		/// <summary>
		/// Performs a comparison and return if left-side value is greater than or equal to right-side value.
		/// </summary>
		/// <param name="x">The left-hand Height value to compare.</param>
		/// <param name="y">The right-hand Int32 value to compare.</param>
		/// <returns>true if left-hand value is greater than or equal to right-side value; otherwise false.</returns>
		public static bool operator >=(Height x, int y) => x.Value >= y;

		/// <summary>
		/// Performs a comparison and return if left-side value is less than or equal to right-side value.
		/// </summary>
		/// <param name="x">The left-hand Height value to compare.</param>
		/// <param name="y">The right-hand Int32 value to compare.</param>
		/// <returns>true if left-hand value is less than or equal to right-side value; otherwise false.</returns>
		public static bool operator <=(Height x, int y) => x.Value <= y;

		#endregion EqualityAndComparison

		#region MathOperations

		/// <summary>
		/// Increments the height value by 1
		/// </summary>
		/// <param name="me">The instance to be used as base value.</param>
		public static Height operator ++(Height me) => new Height(me.Value + 1);

		/// <summary>
		/// Decrements the height value by 1
		/// </summary>
		/// <param name="me">The instance to be used as base value.</param>
		public static Height operator --(Height me) => new Height(me.Value - 1);

		/// <summary>
		/// Unary or binary operator for adding a value to height.
		/// </summary>
		/// <param name="value">The Int32 value.</param>
		/// <param name="height">The height value to be added.</param>
		public static int operator +(int value, Height height) => height.Value + value;

		/// <summary>
		/// Unary or binary operator for substracting a value to height.
		/// </summary>
		/// <param name="value">The Int32 value.</param>
		/// <param name="height">The height value to be substracted from.</param>
		public static int operator -(int value, Height height) => value - height.Value;

		/// <summary>
		/// Unary or binary operator for adding a value to height.
		/// </summary>
		/// <param name="height">The height value to be added.</param>
		/// <param name="value">The Int32 value.</param>
		public static int operator +(Height height, int value) => height.Value + value;

		/// <summary>
		/// Unary or binary operator for substracting a value to height.
		/// </summary>
		/// <param name="height">The height value to be substracted from.</param>
		/// <param name="value">The Int32 value.</param>
		public static int operator -(Height height, int value) => height.Value - value;

		#endregion MathOperations
	}
}
