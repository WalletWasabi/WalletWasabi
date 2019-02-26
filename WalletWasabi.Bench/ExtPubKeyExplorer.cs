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
			var extPubKey = ExtPubKey.Parse("tpubDESeudcLbEHBoH8iw6mL284PXoe2TVmGa3MdQ2gSAkkMj9d1P88LB8wEbVYpigwzurDmDSRsGMKkUsH6vx1anBDCMRzha4YucJfCvEy6z6B");
			var explorer = new ExtPubKeyExplorer(extPubKey, _filters); 
			var index = explorer.UnusedKeys().First();
		}
	}
}