using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Tests.Helpers
{
	public static class WabiSabiFactory
	{
		public static InputRoundSignaturePair CreateInputRoundSignaturePair(Key? key = null, uint256? roundHash = null)
		{
			var rh = roundHash ?? BitcoinFactory.CreateUint256();
			if (key is null)
			{
				using Key k = new();
				return new InputRoundSignaturePair(
						BitcoinFactory.CreateOutPoint(),
						k.SignCompact(rh));
			}
			else
			{
				return new InputRoundSignaturePair(
						BitcoinFactory.CreateOutPoint(),
						key.SignCompact(rh));
			}
		}

		public static IEnumerable<InputRoundSignaturePair> CreateInputRoundSignaturePairs(int count, uint256? roundHash = null)
		{
			for (int i = 0; i < count; i++)
			{
				yield return CreateInputRoundSignaturePair(null, roundHash);
			}
		}

		public static IEnumerable<InputRoundSignaturePair> CreateInputRoundSignaturePairs(IEnumerable<Key> keys, uint256? roundHash = null)
		{
			foreach (var key in keys)
			{
				yield return CreateInputRoundSignaturePair(key, roundHash);
			}
		}
	}
}
