using NBitcoin;
using NBitcoin.DataEncoders;
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
	}

	public static class ExtKeyDataExtensions
	{
		public static string ToWif(this ExtPubKey extPubKey, Network network)
		{
			var data = extPubKey.ToBytes();
			var version = (network == Network.Main)
				? new byte[] { (0x04), (0xB2), (0x47), (0x46) }
				: new byte[] { (0x04), (0x5F), (0x1C), (0xF6) };
			
			return Encoders.Base58Check.EncodeData(version.Concat(data).ToArray());
		}

		public static string ToWif(this ExtKey extKey, Network network)
		{
			var data = extKey.ToBytes();
			var version = (network == Network.Main)
				? new byte[] { (0x04), (0xB2), (0x43), (0x0C) }
				: new byte[] { (0x04), (0x5F), (0x18), (0xBC) };
			
			return Encoders.Base58Check.EncodeData(version.Concat(data).ToArray());
		}
	}
}
