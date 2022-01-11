using System.Linq;
using WalletWasabi.Extensions;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class ImmutableValueSequenceTests
{
	[Fact]
	public void EqualsTest()
	{
		var arr = new[] { 1, 2, 3, 4, 5 };
		var lst = arr.ToList();
		var seq1 = new ImmutableValueSequence<int>(arr);
		var seq2 = new ImmutableValueSequence<int>(arr);

		Assert.Equal(seq1, arr);
		Assert.Equal(seq2, arr);
		Assert.Equal(seq1, lst);
		Assert.Equal(seq2, lst);
		Assert.Equal(seq1, seq2);

		var rec1 = new[]
		{
				new MyRecord("whatever1"),
				new MyRecord("whatever2"),
			}.ToImmutableValueSequence();
		var rec2 = new[]
		{
				new MyRecord("whatever1"),
				new MyRecord("whatever2"),
			}.ToImmutableValueSequence();

		Assert.Equal(rec1, rec2);

		var objs1 = new[]
		{
				new MyClass("whatever1"),
				new MyClass("whatever2"),
			}.ToImmutableValueSequence();
		var objs2 = new[]
		{
				new MyClass("whatever1"),
				new MyClass("whatever2"),
			}.ToImmutableValueSequence();

		Assert.Equal(objs1, objs2);
	}

	public record MyRecord(string Property);
	public class MyClass : IEquatable<MyClass>
	{
		public MyClass(string property)
		{
			Property = property;
		}

		public string Property { get; set; }

		public bool Equals(MyClass? other)
		{
			if (other is null)
			{
				return false;
			}

			return Property == other.Property;
		}

		public override bool Equals(object? obj) => Equals(obj as MyClass);
		public override int GetHashCode() => Property.GetHashCode();
	}
}
