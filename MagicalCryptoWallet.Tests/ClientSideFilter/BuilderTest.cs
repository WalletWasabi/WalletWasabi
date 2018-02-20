using System.Text;
using System.Linq;
using MagicalCryptoWallet.Backend;
using Xunit;

namespace MagicalCryptoWallet.Tests
{
	public class BuilderTest : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		public BuilderTest(SharedFixture fixture)
		{
			SharedFixture = fixture;
		}

		[Fact]
		public void BuildFilterAndMatchValues()
		{
			var names = from name in new[] { "New York", "Amsterdam", "Paris", "Buenos Aires", "La Habana" }
						select Encoding.ASCII.GetBytes(name);

			var key = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
			var filter = GolombRiceFilter.Build(key, 0x10, names);

			// The filter should match all ther values that were added.
			foreach(var name in names)
			{
				Assert.True(filter.Match(name, key), "did't match " + Encoding.ASCII.GetString(name));
			}

			// The filter should NOT match any extra value.
			Assert.False(filter.Match(Encoding.ASCII.GetBytes("Porto Alegre"), key));
			Assert.False(filter.Match(Encoding.ASCII.GetBytes("Madrid"), key));

			// The filter should match because it has one element indexed: Buenos Aires.
			var otherCities = new[] { "La Paz", "Barcelona", "El Cairo", "Buenos Aires", "Asunción" };
			var otherNames = from name in otherCities select Encoding.ASCII.GetBytes(name);
			Assert.True(filter.MatchAny(otherNames, key));

			// The filter should NOT match because it doesn't have any element indexed.
			var otherCities2 = new[] { "La Paz", "Barcelona", "El Cairo", "Córdoba", "Asunción" };
			var otherNames2 = from name in otherCities2 select Encoding.ASCII.GetBytes(name);
			Assert.False(filter.MatchAny(otherNames2, key));
		}
	}
}
