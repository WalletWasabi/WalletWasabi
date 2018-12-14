using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WalletWasabi.Helpers
{
	public static class NBitcoinHelpers
	{
		public static string HashOutpoints(IEnumerable<OutPoint> outPoints)
		{
			var sb = new StringBuilder();
			foreach (OutPoint input in outPoints.OrderBy(x => x.Hash.ToString()).ThenBy(x => x.N))
			{
				sb.Append(ByteHelpers.ToHex(input.ToBytes()));
			}

			return HashHelpers.GenerateSha256Hash(sb.ToString());
		}

		public static BitcoinAddress ParseBitcoinAddress(string address)
		{
			try
			{
				return BitcoinAddress.Create(address, Network.RegTest);
			}
			catch (FormatException)
			{
				try
				{
					return BitcoinAddress.Create(address, Network.TestNet);
				}
				catch (FormatException)
				{
					return BitcoinAddress.Create(address, Network.Main);
				}
			}
		}

		public static Money TakeAReasonableFee(Money outputValue)
		{
			Money fee = Money.Coins(0.002m);
			var remaining = Money.Zero;

			while (true)
			{
				remaining = outputValue - fee;
				if (remaining > Money.Coins(0.00001m))
				{
					break;
				}
				fee = fee.Percentange(50);
			}

			return remaining;
		}
	}
}
