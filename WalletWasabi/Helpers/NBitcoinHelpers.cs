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

		private static readonly Money[] ReasonableFees = new[] {
				Money.Coins(0.002m),
				Money.Coins(0.001m),
				Money.Coins(0.0005m),
				Money.Coins(0.0002m),
				Money.Coins(0.0001m),
				Money.Coins(0.00005m),
				Money.Coins(0.00002m),
				Money.Coins(0.00001m)
			};

		public static Money TakeAReasonableFee(Money inputValue)
		{
			Money half = inputValue / 2;

			foreach (Money fee in ReasonableFees)
			{
				Money diff = inputValue - fee;
				if (diff > half)
				{
					return diff;
				}
			}

			return half;
		}

		public static int CalculateVsizeAssumeSegwit(int inNum, int outNum)
		{
			var origTxSize = (inNum * Constants.P2pkhInputSizeInBytes) + (outNum * Constants.OutputSizeInBytes) + 10;
			var newTxSize = (inNum * Constants.P2wpkhInputSizeInBytes) + (outNum * Constants.OutputSizeInBytes) + 10; // BEWARE: This assumes segwit only inputs!
			var vSize = (int)Math.Ceiling(((3 * newTxSize) + origTxSize) / 4m);
			return vSize;
		}

		public static ExtPubKey BetterParseExtPubKey(string extPubKeyString)
		{
			extPubKeyString = Guard.NotNullOrEmptyOrWhitespace(nameof(extPubKeyString), extPubKeyString, trim: true);

			ExtPubKey epk;
			try
			{
				epk = ExtPubKey.Parse(extPubKeyString);  // Starts with "ExtPubKey": "xpub...
			}
			catch
			{
				// Try hex, Old wallet format was like this.
				epk = new ExtPubKey(ByteHelpers.FromHex(extPubKeyString)); // Starts with "ExtPubKey": "hexbytes...
			}
			return epk;
		}
	}
}
