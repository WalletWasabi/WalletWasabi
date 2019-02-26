using System;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using NBitcoin;
using WalletWasabi.Services;
using WalletWasabi.Backend.Models;
using System.IO;

namespace WalletWasabi.Tests
{
	public class ExtPubKeyExplorerTests
	{
		private ExtKey _extKey;
		private ExtPubKey _extPubKey;
		private Random _random;

		public ExtPubKeyExplorerTests()
		{
			_random = new Random(Seed:6439);

			var mnemonic = new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
			_extKey = mnemonic.DeriveExtKey();
			_extPubKey = _extKey.Derive(new KeyPath("m/84'/0'/0'/1")).Neuter();
		}

		[Fact]
		public void ShouldFindNeverUsedKey()
		{
			var filters = new[] { 
				FilterModel.FromHeightlessLine("000000000000de90e633e1b1330859842795d39018d033044e8b003e8cbf58e4:050a2f58828c9820642769ae320a40", 0)
			};

			var explorer = new ExtPubKeyExplorer(_extPubKey, filters);

			var unusedKeyIndex = explorer.GetIndexFirstUnusedKey();
			Assert.Equal(0, unusedKeyIndex);
		}

		[Fact]
		public void ShouldFindUnsedKeyFirstChunk()
		{
			var filters = new []{
				CreateFiltersWith(GetScripts(0, 10)),
				CreateFiltersWith(GetScripts(10, 10))
			};

			var explorer = new ExtPubKeyExplorer(_extPubKey, filters);

			var unusedKeyIndex = explorer.GetIndexFirstUnusedKey();
			Assert.Equal(20, unusedKeyIndex);
		}

		[Fact]
		public void ShouldFindUnsedKeyAtTheEndOfFirstChunk()
		{
			var filters = new []{
				CreateFiltersWith(GetScripts(0, 500)),
				CreateFiltersWith(GetScripts(500, 499))
			};

			var explorer = new ExtPubKeyExplorer(_extPubKey, filters);

			var unusedKeyIndex = explorer.GetIndexFirstUnusedKey();
			Assert.Equal(999, unusedKeyIndex);
		}

		[Fact]
		public void ShouldFindUnsedKeyAnywhereFirstChunk()
		{
			var filters = new []{
				CreateFiltersWith(GetScripts(0,  1)),
				CreateFiltersWith(GetScripts(0,  2)),
				CreateFiltersWith(GetScripts(0, 27)),
				CreateFiltersWith(GetScripts(27, 3)),
			};

			var explorer = new ExtPubKeyExplorer(_extPubKey, filters);

			var unusedKeyIndex = explorer.GetIndexFirstUnusedKey();
			Assert.Equal(30, unusedKeyIndex);
		}

		[Fact]
		public void ShouldFindUnsedKeySecondChunk()
		{
			var filters = new []{
				CreateFiltersWith(GetScripts(  0, 250)),
				CreateFiltersWith(GetScripts(250, 250)),
				CreateFiltersWith(GetScripts(500, 250)),
				CreateFiltersWith(GetScripts(750, 250))
			};

			var explorer = new ExtPubKeyExplorer(_extPubKey, filters);

			var unusedKeyIndex = explorer.GetIndexFirstUnusedKey();
			Assert.Equal(1_000, unusedKeyIndex);
		}

		[Fact]
		public void ShouldFindUnsedKeyFarFarAway()
		{
			var filters = Enumerable.Range(0, 100).Select(x=>CreateFiltersWith(GetScripts(x*250, 250))).ToArray();

			var explorer = new ExtPubKeyExplorer(_extPubKey, filters);

			var unusedKeyIndex = explorer.GetIndexFirstUnusedKey();
			Assert.Equal(25_000, unusedKeyIndex);
		}

		[Fact]
		public void RealWorldCase()
		{
			var filters = new List<FilterModel>(100_000);
			using(var file = File.OpenRead("../../../../WalletWasabi.Bench/IndexTestNet.bin"))
			while(file.Position < file.Length)
				filters.Add(FilterModel.FromStream(file, 0));

			var extPubKey = ExtPubKey.Parse("tpubDESeudcLbEHBoH8iw6mL284PXoe2TVmGa3MdQ2gSAkkMj9d1P88LB8wEbVYpigwzurDmDSRsGMKkUsH6vx1anBDCMRzha4YucJfCvEy6z6B");
			var explorer = new ExtPubKeyExplorer(extPubKey, filters, 3); 
			var index = explorer.GetIndexFirstUnusedKey();
			Console.WriteLine($"first index: m/84'/0'/0'/0/{index}");
			// Review: The Wallet file indicates that the first Locked index is 48, however increasing the chunkSize makes the
			// method to return wrong index.
		}


		private FilterModel CreateFiltersWith(IEnumerable<byte[]> scripts)
		{
			var keyBuffer = new byte[32];
			_random.NextBytes(keyBuffer);
			var blockHash = new uint256(keyBuffer);

			var builder = new GolombRiceFilterBuilder()
				.SetKey(blockHash)
				.SetP(20);

			builder.AddEntries(scripts);
			return new FilterModel{
				BlockHeight = 0,
				BlockHash = new uint256(blockHash),
				Filter = builder.Build()
			};
		}

		private byte[][] GetScripts(int offset, int count)
		{
			var scripts = new byte[count][];
			for(var i=0; i < count; i++)
			{
				var pubKey = _extPubKey.Derive((uint)(offset + i)).PubKey;
				var bytes = pubKey.WitHash.ScriptPubKey.ToCompressedBytes();
				scripts[i] = bytes;
			}
			return scripts;
		}
	}
}