using System;
using System.Linq;
using System.Collections.Generic;
using NBitcoin;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using WalletWasabi.Services;
using WalletWasabi.Backend.Models;
using System.IO;

namespace WalletWasabi.Benck
{
	[ClrJob(baseline: true), CoreJob]
	[RPlotExporter, RankColumn]
	public class SearchingUnusedAddresses
	{
		[Params(21)]
		public int ChunkSize;

		private List<FilterModel> _filters;

		[GlobalSetup]
		public void Setup()
		{
			_filters = new List<FilterModel>(500_000);
			using(var file = File.OpenRead("IndexTestNet.bin"))
			while(file.Position < file.Length)
				_filters.Add(FilterModel.FromStream(file, 0));
		}

		[Benchmark]
		public void FindFirstUnusedKey()
		{
			var extPubKey = ExtPubKey.Parse("tpubDHATEWfRGN6UyE9TMGASsojBxrvWsaDc6zuVDiiAspjJrHsUUuR7eV7KyFM8RYtAqHnTjqBd7BigU4qy1CeAdkHw3Y4ocmT63DUsKVEwgWu");
			var explorer = new ExtPubKeyExplorer(extPubKey, _filters, ChunkSize); 
			var index = explorer.GetIndexFirstUnusedKey();
			Console.WriteLine($"first index: m/84'/0'/0'/0/{index}");
		}
	}
}