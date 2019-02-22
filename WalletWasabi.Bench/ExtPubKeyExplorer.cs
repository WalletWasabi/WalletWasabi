using System;
using System.Linq;
using System.Collections.Generic;
using NBitcoin;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using WalletWasabi.Services;
using WalletWasabi.Backend.Models;

namespace WalletWasabi.Benck
{
	[ClrJob(baseline: true), CoreJob]
	[RPlotExporter, RankColumn]
	public class SearchingUnusedAddresses
	{
		private ExtPubKey _extPubKey;
		private FilterModel[] _filters;

		[Params(100, 1_000)]
		public int USED_ADDRS;

		[Params(2_000, 5_000)]
		public int OUTUPS;

		[Params(20_000)]
		public int FILTERS;

		[GlobalSetup]
		public void Setup()
		{
			var random = new Random(Seed:145);

			var mnemonic = new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");
			var extKey = mnemonic.DeriveExtKey();
			_extPubKey = extKey.Derive(new KeyPath("m/84'/0'/1")).Neuter();
			var myScripts = new byte[USED_ADDRS][];
			var keyBuffer = new byte[32];
			for(var i=0; i<USED_ADDRS; i++)
			{
				myScripts[i] = _extPubKey.Derive((uint)i).PubKey.GetSegwitAddress(Network.Main).ScriptPubKey.ToBytes();
			}

			var lastUsed = 0;
			_filters = new FilterModel[FILTERS];
			for(var f=0; f < FILTERS; f++)
			{
				random.NextBytes(keyBuffer);
				var blockHash = new uint256(keyBuffer);

				var builder = new GolombRiceFilterBuilder()
					.SetKey(blockHash)
					.SetP(20);

				var itemsInFilter = new List<byte[]>();
				for (var j = 0; j < OUTUPS; j++)
				{
					var data = new byte[random.Next(20, 30)];
					random.NextBytes(data);
					itemsInFilter.Add(data);
				}
				var doIHaveCoinsInTheBlock = random.Next(0, 99) < 10;
				if(doIHaveCoinsInTheBlock && lastUsed < USED_ADDRS)
				{
					itemsInFilter.Add(myScripts[lastUsed++]);
				}
				builder.AddEntries(itemsInFilter);
				_filters[f] = new FilterModel{
					BlockHeight = f,
					BlockHash = new uint256(blockHash),
					Filter = builder.Build()
				};
			}
		}

		[Benchmark]
		public BitcoinWitPubKeyAddress[] FindFirstUnusedKey()
		{
			var result = ExtPubKeyExplorer.GetUnusedBech32Keys(1, true, new BitcoinExtPubKey(_extPubKey, Network.Main), _filters);
			return result?.ToArray();
		}
	}
}