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
				sb.Append(NBitcoin.DataEncoders.Encoders.Hex.EncodeData(input.ToBytes()));
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
	}
}
