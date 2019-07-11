using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;

namespace WalletWasabi.Services
{
	public class ExtPubKeyExplorer
	{
		/// <summary>
		/// WARNING: ONLY CHECKS CONFIRMED KEYPATHS
		/// </summary>
		public static IEnumerable<BitcoinWitPubKeyAddress> GetUnusedBech32Keys(int count, bool isInternal, BitcoinExtPubKey bitcoinExtPubKey, IEnumerable<FilterModel> filters)
		{
			var change = isInternal ? 1 : 0;
			var filterArray = filters.ToArray();

			var startingKey = 0; // Where to start getting keys one by one.
			int i = 0;
			while (true)
			{
				if (Match(bitcoinExtPubKey, change, i, filterArray, out _))
				{
					startingKey = i + 1;
					if (i == 0)
					{
						i = 1;
					}
					else
					{
						i *= 2;
					}
				}
				else
				{
					break;
				}
			}

			i = startingKey;
			int returnedNum = 0;
			while (true)
			{
				if (!Match(bitcoinExtPubKey, change, i, filterArray, out PubKey pubKey))
				{
					yield return pubKey.GetSegwitAddress(bitcoinExtPubKey.Network);
					returnedNum++;
					if (returnedNum >= count)
					{
						yield break;
					}
				}

				i++;
			}
		}

		public static bool Match(BitcoinExtPubKey bitcoinExtPubKey, int change, int i, FilterModel[] filterArray, out PubKey pubKey)
		{
			KeyPath path = new KeyPath($"{change}/{i}");
			pubKey = bitcoinExtPubKey.ExtPubKey.Derive(path).PubKey;
			byte[] bytes = pubKey.WitHash.ScriptPubKey.ToCompressedBytes();

			foreach (var filterModel in filterArray)
			{
				bool? matchFoundNow = filterModel.Filter?.Match(bytes, filterModel.FilterKey);
				if (matchFoundNow is true)
				{
					return true;
				}
			}

			return false;
		}
	}
}
