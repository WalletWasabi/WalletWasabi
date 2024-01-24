using System.Linq;
using WalletWasabi.Extensions;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class LinqExtensionsTests
{
	[Fact]
	public void CombinationsWithoutRepetitionOfFixedLength()
	{
		var combinations = Enumerable.Range(0, 5).CombinationsWithoutRepetition(ofLength: 3);
		var asString = combinations.Select(x => string.Join(", ", x)).ToArray();

		Assert.Equal(10, asString.Length);
		Assert.Equal(combinations.Count(), combinations.Distinct().Count());

		Assert.Equal("0, 1, 2", asString[0]);
		Assert.Equal("0, 1, 3", asString[1]);
		Assert.Equal("0, 1, 4", asString[2]);
		Assert.Equal("0, 2, 3", asString[3]);
		Assert.Equal("0, 2, 4", asString[4]);
		Assert.Equal("0, 3, 4", asString[5]);
		Assert.Equal("1, 2, 3", asString[6]);
		Assert.Equal("1, 2, 4", asString[7]);
		Assert.Equal("1, 3, 4", asString[8]);
		Assert.Equal("2, 3, 4", asString[9]);
	}

	[Fact]
	public void CombinationsWithoutRepetitionUpToLength()
	{
		var combinations = Enumerable.Range(0, 5).CombinationsWithoutRepetition(ofLength: 1, upToLength: 4);
		var asString = combinations.Select(x => string.Join(", ", x)).ToArray();

		Assert.Equal(30, asString.Length);
		Assert.Equal(combinations.Count(), combinations.Distinct().Count());

		var i = 0;
		Assert.Equal("0", asString[i++]);
		Assert.Equal("1", asString[i++]);
		Assert.Equal("2", asString[i++]);
		Assert.Equal("3", asString[i++]);
		Assert.Equal("4", asString[i++]);
		Assert.Equal("0, 1", asString[i++]);
		Assert.Equal("0, 2", asString[i++]);
		Assert.Equal("0, 3", asString[i++]);
		Assert.Equal("0, 4", asString[i++]);
		Assert.Equal("1, 2", asString[i++]);
		Assert.Equal("1, 3", asString[i++]);
		Assert.Equal("1, 4", asString[i++]);
		Assert.Equal("2, 3", asString[i++]);
		Assert.Equal("2, 4", asString[i++]);
		Assert.Equal("3, 4", asString[i++]);
		Assert.Equal("0, 1, 2", asString[i++]);
		Assert.Equal("0, 1, 3", asString[i++]);
		Assert.Equal("0, 1, 4", asString[i++]);
		Assert.Equal("0, 2, 3", asString[i++]);
		Assert.Equal("0, 2, 4", asString[i++]);
		Assert.Equal("0, 3, 4", asString[i++]);
		Assert.Equal("1, 2, 3", asString[i++]);
		Assert.Equal("1, 2, 4", asString[i++]);
		Assert.Equal("1, 3, 4", asString[i++]);
		Assert.Equal("2, 3, 4", asString[i++]);
		Assert.Equal("0, 1, 2, 3", asString[i++]);
		Assert.Equal("0, 1, 2, 4", asString[i++]);
		Assert.Equal("0, 1, 3, 4", asString[i++]);
		Assert.Equal("0, 2, 3, 4", asString[i++]);
		Assert.Equal("1, 2, 3, 4", asString[i++]);
	}

	[Fact]
	public void CombinationsWithoutRepetitionZeroLength()
	{
		AssertAsync.CompletesIn(5, () => Enumerable.Range(0, 32).CombinationsWithoutRepetition(ofLength: 0).ToArray());
	}

	[Fact]
	public void ZippingTests()
	{
		var collection1 = new int[] { 1, 3, 5, 14 };
		var collection2 = new int[] { 7, 11, 13 };
		Assert.ThrowsAny<InvalidOperationException>(() => collection1.ZipForceEqualLength(collection2));
		collection1 = new int[] { 1, 3, 5, 14 };
		collection2 = new int[] { 7, 11, 13, 1, 2 };
		Assert.ThrowsAny<InvalidOperationException>(() => collection1.ZipForceEqualLength(collection2));
		collection1 = new int[] { 1, 3, 5, 14, 3 };
		collection2 = new int[] { 7, 11, 13, 1, 2 };
		var tuple = collection1.ZipForceEqualLength(collection2).ToArray();
		for (int i = 0; i < tuple.Length; i++)
		{
			Assert.Equal(collection1[i], tuple[i].Item1);
			Assert.Equal(collection2[i], tuple[i].Item2);
		}
	}

	[Fact]
	public void MaxOrDefault()
	{
		Assert.Equal(10, Array.Empty<int>().MaxOrDefault(defaultValue: 10));
		Assert.Equal(1, new int[] { 1 }.MaxOrDefault(defaultValue: 10));
		Assert.Equal(2, new int[] { 1, 2 }.MaxOrDefault(defaultValue: 10));
		Assert.Equal(3, new int[] { 1, 2, 3 }.MaxOrDefault(defaultValue: 10));
		Assert.Equal(4, new int[] { 1, 2, 3, 4 }.MaxOrDefault(defaultValue: 10));
		Assert.Equal(4, new int[] { 4, 3, 2, 1 }.MaxOrDefault(defaultValue: 10));
		Assert.Equal(4, new int[] { 4, 3, 2 }.MaxOrDefault(defaultValue: 10));
		Assert.Equal(4, new int[] { 4, 3 }.MaxOrDefault(defaultValue: 10));
		Assert.Equal(4, new int[] { 4 }.MaxOrDefault(defaultValue: 10));
	}
}
