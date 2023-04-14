using System.Collections.Generic;
using WalletWasabi.Extensions;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class ListExtensionTests
{
	[Fact]
	public void InsertSorted_Orders_Items_Correctly()
	{
		var actual = new List<int>();

		actual.InsertSorted(5);
		actual.InsertSorted(4);
		actual.InsertSorted(3);
		actual.InsertSorted(2);
		actual.InsertSorted(1);
		actual.InsertSorted(0);

		var expected = new List<int> { 0, 1, 2, 3, 4, 5 };

		Assert.Equal(expected, actual);
	}

	[Fact]
	public void InsertSorted_Orders_Items_Correctly_Allowing_Duplicates()
	{
		var actual = new List<int>();

		actual.InsertSorted(5);
		actual.InsertSorted(4);
		actual.InsertSorted(3);
		actual.InsertSorted(2);
		actual.InsertSorted(5, false);
		actual.InsertSorted(1);
		actual.InsertSorted(0);

		var expected = new List<int> { 0, 1, 2, 3, 4, 5, 5 };

		Assert.Equal(expected, actual);
	}

	[Fact]
	public void BinarySearch_Uses_CompareTo()
	{
		var actual = new List<ReverseComparable>();

		actual.InsertSorted(new ReverseComparable(0));
		actual.InsertSorted(new ReverseComparable(1));
		actual.InsertSorted(new ReverseComparable(2));
		actual.InsertSorted(new ReverseComparable(3));
		actual.InsertSorted(new ReverseComparable(4));

		var expected = new List<ReverseComparable>
			{
				new ReverseComparable(4),
				new ReverseComparable(3),
				new ReverseComparable(2),
				new ReverseComparable(1),
				new ReverseComparable(0)
			};

		Assert.Equal(expected, actual);
	}

	private class ReverseComparable : IComparable<ReverseComparable>
	{
		public ReverseComparable(int value)
		{
			Value = value;
		}

		public int Value { get; }

		public int CompareTo(ReverseComparable? other)
		{
			var nonNullOther = other ?? throw new ArgumentNullException(nameof(other));
			return nonNullOther.Value.CompareTo(Value);
		}
	}
}
