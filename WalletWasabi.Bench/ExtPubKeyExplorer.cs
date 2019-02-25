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
			var extPubKey = ExtPubKey.Parse("tpubD6NzVbkrYhZ4YcNe2sjvpcdPzEgZuXQyWSnVjxmYQLCXAce7UKVtbftWaUGyCKrdXMy2uWy7DcTqMTXJWmgFJZd2T1BzHRPm7cHvzBTmeed");
			var scriptProvider = new ScriptPubKeyProvider(extPubKey);
			var explorer = new ExtPubKeyExplorer(scriptProvider, _filters); 
			var index = explorer.GetIndexFirstUnusedKey();
			Console.WriteLine($"first index: m/84'/0'/0'/0/{index}");
		}
	}
}