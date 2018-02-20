using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GMagicalCryptoWallet.Tests;
using MagicalCryptoWallet.Backend;
using NBitcoin.Crypto;
using Xunit;


namespace MagicalCryptoWallet.Tests
{
	public class PersistenceTest : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		public PersistenceTest(SharedFixture fixture)
		{
			SharedFixture = fixture;
		}

		[Fact]
		public void CreateStore()
		{
			const byte P = 20;
			const int blockCount = 100;
			const int maxBlockSize = 4 * 1000 * 1000;
			const int avgTxSize = 250; // Currently the average is around 1kb.
			const int txoutCountPerBlock = maxBlockSize / avgTxSize;
			const int avgTxoutPushDataSize = 20; // P2PKH scripts has 20 bytes.

			var key = new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15};

			// Generation of data to be added into the filter
			var random = new Random();
			var dataDirectory = new DirectoryInfo("./data");
			if (dataDirectory.Exists)
			{
				foreach (var fileInfo in dataDirectory.GetFiles())
				{
					fileInfo.Delete();
				}
			}

			var blocks = new List<GolombRiceFilter>(blockCount);
			using (var repo = GcsFilterRepository.Open("./data"))
			{
				for (var i = 0; i < blockCount; i++)
				{
					var txouts = new List<byte[]>(txoutCountPerBlock);
					for (var j = 0; j < txoutCountPerBlock; j++)
					{
						var pushDataBuffer = new byte[avgTxoutPushDataSize];
						random.NextBytes(pushDataBuffer);
						txouts.Add(pushDataBuffer);
					}

					var filter = GolombRiceFilter.Build(key, P, txouts);
					blocks.Add(filter);
					repo.Put(Hashes.Hash256(filter.Data.ToByteArray()), filter);
				}
			}

			using (var repo = GcsFilterRepository.Open("./data"))
			{
				var blockIndexes = Enumerable.Range(0, blockCount).ToList();
				blockIndexes.Shuffle();

				foreach (var blkIndx in blockIndexes)
				{
					var block = blocks[blkIndx];
					var blockFilter = block;
					var blockFilterId = Hashes.Hash256(blockFilter.Data.ToByteArray());
					var savedFilter = repo.Get(blockFilterId);
					var savedFilterId = Hashes.Hash256(savedFilter.Data.ToByteArray());
					Assert.Equal(blockFilterId, savedFilterId);
				}
			}
		}
	}
}
