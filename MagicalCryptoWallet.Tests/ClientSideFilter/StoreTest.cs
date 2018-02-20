using System;
using System.IO;
using System.Linq;
using MagicalCryptoWallet.Backend;
using Xunit;

namespace MagicalCryptoWallet.Tests
{
	public class StoreTest : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		public StoreTest(SharedFixture fixture)
		{
			SharedFixture = fixture;
		}

		[Fact]
		public void Test1()
		{
			var stream = new MemoryStream();
			var filterStore = new GcsFilterStore(stream);
			filterStore.Put(new GolombRiceFilter(new FastBitArray(), 20, 10));
			filterStore.Put(new GolombRiceFilter(new FastBitArray(), 20, 35));

			stream.Seek(0, SeekOrigin.Begin);
			var filters = filterStore.ToArray();
			Assert.Equal(2, filters.Length);
			Assert.Equal(10, filters[0].N);
			Assert.Equal(35, filters[1].N);
		}
	}
}
