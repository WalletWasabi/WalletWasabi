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
		public static InputRoundSignaturePair CreateInputRoundSignaturePair()
		{
			using Key key = new();
			return new InputRoundSignaturePair(
					BitcoinFactory.CreateOutPoint(),
					key.SignCompact(BitcoinFactory.CreateUint256()));
		}

		public static IEnumerable<InputRoundSignaturePair> CreateInputRoundSignaturePairs(int count)
		{
			for (int i = 0; i < count; i++)
			{
				yield return CreateInputRoundSignaturePair();
			}
		}
	}
}
