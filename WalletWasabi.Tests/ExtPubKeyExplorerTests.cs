using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalletWasabi.Backend.Models;
using WalletWasabi.KeyManagement;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests
{
	public class ExtPubKeyExplorerTests
	{
		private ExtKey ExtKey { get; }
		private ExtPubKey ExtPubKey { get; }
		private Random Random { get; }

		public ExtPubKeyExplorerTests()
		{
			Random = new Random(Seed: 6439);

			var mnemonic = new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
			ExtKey = mnemonic.DeriveExtKey();
			ExtPubKey = ExtKey.Derive(new KeyPath($"{KeyManager.DefaultAccountKeyPath.ToString(true, "h")}/1/0")).Neuter();
		}

		[Fact]
		public void ShouldFindNeverUsedKey()
		{
			var filters = new[] {
				FilterModel.FromHeightlessLine("000000000000de90e633e1b1330859842795d39018d033044e8b003e8cbf58e4:050a2f58828c9820642769ae320a40", 0)
			};

			var unusedKeyIndex = ExtPubKeyExplorer.GetUnusedBech32Keys(1, true, ExtPubKey.GetWif(Network.Main), filters).First().ScriptPubKey.ToCompressedBytes();

			Assert.Equal(DerivateScript(true, 0), unusedKeyIndex);
		}

		[Fact]
		public void ShouldFindUnsedKeyFirstChunk()
		{
			var filters = new[]{
				CreateFiltersWith(GetScripts(false, 0, 10)),
				CreateFiltersWith(GetScripts(false, 10, 10))
			};

			var unusedKeyIndex = ExtPubKeyExplorer.GetUnusedBech32Keys(1, false, ExtPubKey.GetWif(Network.Main), filters).First().ScriptPubKey.ToCompressedBytes();

			Assert.Equal(DerivateScript(false, 20), unusedKeyIndex);
		}

		[Fact]
		public void ShouldFindUnsedKeyAtTheEndOfFirstChunk()
		{
			var filters = new[]{
				CreateFiltersWith(GetScripts(true, 0, 500)),
				CreateFiltersWith(GetScripts(true, 500, 499))
			};

			var unusedKeyIndex = ExtPubKeyExplorer.GetUnusedBech32Keys(1, true, ExtPubKey.GetWif(Network.Main), filters).First().ScriptPubKey.ToCompressedBytes();
			Assert.Equal(DerivateScript(true, 999), unusedKeyIndex);
		}

		[Fact]
		public void ShouldFindUnsedKeyAnywhereFirstChunk()
		{
			var filters = new[]{
				CreateFiltersWith(GetScripts(true, 0,  1)),
				CreateFiltersWith(GetScripts(true, 0,  2)),
				CreateFiltersWith(GetScripts(true, 0, 27)),
				CreateFiltersWith(GetScripts(true, 27, 3)),
			};

			var unusedKeyIndex = ExtPubKeyExplorer.GetUnusedBech32Keys(1, true, ExtPubKey.GetWif(Network.Main), filters).First().ScriptPubKey.ToCompressedBytes();

			Assert.Equal(DerivateScript(true, 30), unusedKeyIndex);
		}

		[Fact]
		public void ShouldFindUnsedKeySecondChunk()
		{
			var filters = new[]{
				CreateFiltersWith(GetScripts(true,   0, 250)),
				CreateFiltersWith(GetScripts(true, 250, 250)),
				CreateFiltersWith(GetScripts(true, 500, 250)),
				CreateFiltersWith(GetScripts(true, 750, 250))
			};

			var unusedKeyIndex = ExtPubKeyExplorer.GetUnusedBech32Keys(1, true, ExtPubKey.GetWif(Network.Main), filters).First().ScriptPubKey.ToCompressedBytes();

			Assert.Equal(DerivateScript(true, 1_000), unusedKeyIndex);
		}

		[Fact]
		public void ShouldFindUnsedKeyFarFarAway()
		{
			var filters = Enumerable.Range(0, 100).Select(x => CreateFiltersWith(GetScripts(true, x * 250, 250))).ToArray();

			var unusedKeyIndex = ExtPubKeyExplorer.GetUnusedBech32Keys(1, true, ExtPubKey.GetWif(Network.Main), filters).First().ScriptPubKey.ToCompressedBytes();
			Assert.Equal(DerivateScript(true, 25_000), unusedKeyIndex);
		}

		private FilterModel CreateFiltersWith(IEnumerable<byte[]> scripts)
		{
			var keyBuffer = new byte[32];
			Random.NextBytes(keyBuffer);
			var blockHash = new uint256(keyBuffer);

			var builder = new GolombRiceFilterBuilder()
				.SetKey(blockHash)
				.SetP(20);

			builder.AddEntries(scripts);
			return new FilterModel
			{
				BlockHeight = 0,
				BlockHash = new uint256(blockHash),
				Filter = builder.Build()
			};
		}

		private byte[][] GetScripts(bool isInternal, int offset, int count)
		{
			var change = isInternal ? 1 : 0;
			var scripts = new byte[count][];
			for (var i = 0; i < count; i++)
			{
				var pubKey = ExtPubKey.Derive(change, false).Derive(offset + i, false).PubKey;
				var bytes = pubKey.WitHash.ScriptPubKey.ToCompressedBytes();
				scripts[i] = bytes;
			}
			return scripts;
		}

		private IEnumerable<byte> DerivateScript(bool isInternal, int index)
		{
			var change = isInternal ? 1 : 0;
			return ExtPubKey.Derive(change, false).Derive(index, false).PubKey.WitHash.ScriptPubKey.ToCompressedBytes();
		}
	}
}
