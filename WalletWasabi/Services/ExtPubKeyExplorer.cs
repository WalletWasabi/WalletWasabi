using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;

namespace WalletWasabi.Services
{
	public static class ExtPubKeyExplorer
	{
		public static IEnumerable<BitcoinWitPubKeyAddress> GetUnusedBech32Keys(int count, bool isInternal, BitcoinExtPubKey bitcoinExtPubKey, IEnumerable<FilterModel> filters)
		{
			var change = isInternal ? 1u : 0u;
			var filterArray = filters.ToArray();

			const int MaxScanDepth = 1_000;
			var path = new KeyPath($"{change}/0");
			var scripts = new byte[MaxScanDepth][];
			for(var i=0; i<MaxScanDepth; i++)
			{
				var pubKey = bitcoinExtPubKey.ExtPubKey.Derive(change).PubKey;
				var bytes = pubKey.WitHash.ScriptPubKey.ToCompressedBytes();
				scripts[i] = bytes;
			}

			var found = -1;
			var begin=0;
			var size = MaxScanDepth-1;
			while(found>-1 && size > 0)
			{
				var mid = size / 2;
				var lh = new ArraySegment<byte[]>(scripts, begin, mid);
				var rh = new ArraySegment<byte[]>(scripts, begin + mid, mid);

				var flh = false;
				var frh = false;
				foreach (var filterModel in filterArray.Where(x=>x.Filter != null))
				{
					flh = filterModel.Filter.MatchAny(lh, filterModel.FilterKey);
					if (flh) break;
				}
				if(!flh)
				{
					return scripts.Skip(begin).Take(count)
						.Select(x=>new Script(x).WitHash.GetAddress(bitcoinExtPubKey.Network))
						.Cast<BitcoinWitPubKeyAddress>();
				}

				foreach (var filterModel in filterArray.Where(x=>x.Filter != null))
				{
					frh = filterModel.Filter.MatchAny(rh, filterModel.FilterKey);
					if (frh) break;
				}

				if(flh && !frh)
				{
					size /= 2;
				}
				else if( flh && frh)
				{
					begin = mid; 
				}
			}

			return null;
		}
	}
}
