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
		private ScriptPubKeyProvider _scriptProvider;
		private Random _random;

		public ExtPubKeyExplorerTests()
		{
			_random = new Random(Seed:6439);

			var mnemonic = new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
			_extKey = mnemonic.DeriveExtKey();
		}

		[Fact]
		public void ShouldFindNeverUsedKey()
		{
			var extPubKey = _extKey.Derive(new KeyPath("m/84'/0'/1")).Neuter();
			var scriptProvider = new ScriptPubKeyProvider(extPubKey);
			var filters = new[] { 
				FilterModel.FromHeightlessLine("000000000000de90e633e1b1330859842795d39018d033044e8b003e8cbf58e4:050a2f58828c9820642769ae320a40", 0)
			};

			var explorer = new ExtPubKeyExplorer(scriptProvider, filters);

			var unusedKeyIndex = explorer.GetIndexFirstUnusedKey();
			Assert.Equal(0, unusedKeyIndex);
		}

		[Fact]
		public void ShouldFindUnsedKeyFirstChunk()
		{
			var extPubKey = _extKey.Derive(new KeyPath("m/84'/0'/1")).Neuter();
			var scriptProvider = new ScriptPubKeyProvider(extPubKey);
			var filters = new []{
				CreateFiltersWith(scriptProvider.GetScripts(0, 10)),
				CreateFiltersWith(scriptProvider.GetScripts(10, 10))
			};

			var explorer = new ExtPubKeyExplorer(scriptProvider, filters);

			var unusedKeyIndex = explorer.GetIndexFirstUnusedKey();
			Assert.Equal(20, unusedKeyIndex);
		}

		[Fact]
		public void ShouldFindUnsedKeyAtTheEndOfFirstChunk()
		{
			var extPubKey = _extKey.Derive(new KeyPath("m/84'/0'/1")).Neuter();
			var scriptProvider = new ScriptPubKeyProvider(extPubKey);
			var filters = new []{
				CreateFiltersWith(scriptProvider.GetScripts(0, 500)),
				CreateFiltersWith(scriptProvider.GetScripts(500, 499))
			};

			var explorer = new ExtPubKeyExplorer(scriptProvider, filters);

			var unusedKeyIndex = explorer.GetIndexFirstUnusedKey();
			Assert.Equal(999, unusedKeyIndex);
		}

		[Fact]
		public void ShouldFindUnsedKeySecondChunk()
		{
			var extPubKey = _extKey.Derive(new KeyPath("m/84'/0'/1")).Neuter();
			var scriptProvider = new ScriptPubKeyProvider(extPubKey);
			var filters = new []{
				CreateFiltersWith(scriptProvider.GetScripts(  0, 250)),
				CreateFiltersWith(scriptProvider.GetScripts(250, 250)),
				CreateFiltersWith(scriptProvider.GetScripts(500, 250)),
				CreateFiltersWith(scriptProvider.GetScripts(750, 250))
			};

			var explorer = new ExtPubKeyExplorer(scriptProvider, filters);

			var unusedKeyIndex = explorer.GetIndexFirstUnusedKey();
			Assert.Equal(1_000, unusedKeyIndex);
		}

		[Fact]
		public void RealWorldCase()
		{
			var filters = new List<FilterModel>(500_000);
			using(var file = File.OpenRead("/home/lontivero/GitHub/WalletWasabi/WalletWasabi.Bench/IndexTestNet.bin"))
			while(file.Position < file.Length)
				filters.Add(FilterModel.FromStream(file, 0));

			var extPubKey = ExtPubKey.Parse("tpubD6NzVbkrYhZ4YcNe2sjvpcdPzEgZuXQyWSnVjxmYQLCXAce7UKVtbftWaUGyCKrdXMy2uWy7DcTqMTXJWmgFJZd2T1BzHRPm7cHvzBTmeed");
			var scriptProvider = new ScriptPubKeyProvider(extPubKey);
			var explorer = new ExtPubKeyExplorer(scriptProvider, filters); 
			var index = explorer.GetIndexFirstUnusedKey();
			Console.WriteLine($"first index: m/84'/0'/0'/0/{index}");
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
	}
}