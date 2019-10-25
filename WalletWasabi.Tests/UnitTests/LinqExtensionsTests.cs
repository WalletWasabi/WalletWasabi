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
			var AsString = combinations.Select(x => string.Join(", ", x)).ToArray();

			Assert.Equal(10, AsString.Length);
			Assert.Equal(combinations.Count(), combinations.Distinct().Count());

			Assert.Equal("0, 1, 2", AsString[0]);
			Assert.Equal("0, 1, 3", AsString[1]);
			Assert.Equal("0, 1, 4", AsString[2]);
			Assert.Equal("0, 2, 3", AsString[3]);
			Assert.Equal("0, 2, 4", AsString[4]);
			Assert.Equal("0, 3, 4", AsString[5]);
			Assert.Equal("1, 2, 3", AsString[6]);
			Assert.Equal("1, 2, 4", AsString[7]);
			Assert.Equal("1, 3, 4", AsString[8]);
			Assert.Equal("2, 3, 4", AsString[9]);
		}

		[Fact]
		public void CombinationsWithoutRepetitionUpToLenth()
		{
			var combinations = Enumerable.Range(0, 5).CombinationsWithoutRepetition(ofLength: 1, upToLength: 4);
			var AsString = combinations.Select(x => string.Join(", ", x)).ToArray();

			Assert.Equal(30, AsString.Length);
			Assert.Equal(combinations.Count(), combinations.Distinct().Count());

			var i = 0;
			Assert.Equal("0", AsString[i++]);
			Assert.Equal("1", AsString[i++]);
			Assert.Equal("2", AsString[i++]);
			Assert.Equal("3", AsString[i++]);
			Assert.Equal("4", AsString[i++]);
			Assert.Equal("0, 1", AsString[i++]);
			Assert.Equal("0, 2", AsString[i++]);
			Assert.Equal("0, 3", AsString[i++]);
			Assert.Equal("0, 4", AsString[i++]);
			Assert.Equal("1, 2", AsString[i++]);
			Assert.Equal("1, 3", AsString[i++]);
			Assert.Equal("1, 4", AsString[i++]);
			Assert.Equal("2, 3", AsString[i++]);
			Assert.Equal("2, 4", AsString[i++]);
			Assert.Equal("3, 4", AsString[i++]);
			Assert.Equal("0, 1, 2", AsString[i++]);
			Assert.Equal("0, 1, 3", AsString[i++]);
			Assert.Equal("0, 1, 4", AsString[i++]);
			Assert.Equal("0, 2, 3", AsString[i++]);
			Assert.Equal("0, 2, 4", AsString[i++]);
			Assert.Equal("0, 3, 4", AsString[i++]);
			Assert.Equal("1, 2, 3", AsString[i++]);
			Assert.Equal("1, 2, 4", AsString[i++]);
			Assert.Equal("1, 3, 4", AsString[i++]);
			Assert.Equal("2, 3, 4", AsString[i++]);
			Assert.Equal("0, 1, 2, 3", AsString[i++]);
			Assert.Equal("0, 1, 2, 4", AsString[i++]);
			Assert.Equal("0, 1, 3, 4", AsString[i++]);
			Assert.Equal("0, 2, 3, 4", AsString[i++]);
			Assert.Equal("1, 2, 3, 4", AsString[i++]);
		}
	}
}
