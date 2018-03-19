using System;
using System.IO;
using System.Linq;
using MagicalCryptoWallet.Backend;
using NBitcoin;
using NBitcoin.DataEncoders;
using Xunit;

namespace MagicalCryptoWallet.Tests
{
	public class StoreTest : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		public StoreTest(SharedFixture sharedFixture)
		{
			SharedFixture = sharedFixture;
		}

		[Fact]
		public void Test1()
		{
			var stream = new MemoryStream();
			var filterStore = new GcsFilterStore(stream);
			filterStore.Put(new GolombRiceFilter(new FastBitArray(), 10, 20));
			filterStore.Put(new GolombRiceFilter(new FastBitArray(), 35, 20));

			stream.Seek(0, SeekOrigin.Begin);
			var filters = filterStore.ToArray();
			Assert.Equal(2, filters.Length);
			Assert.Equal(10, filters[0].N);
			Assert.Equal(35, filters[1].N);
		}
	}
}
