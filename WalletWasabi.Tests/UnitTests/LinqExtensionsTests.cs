using System.Linq;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
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
		public void CombinationsWithoutRepetitionUpToLenth()
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
	}
}
