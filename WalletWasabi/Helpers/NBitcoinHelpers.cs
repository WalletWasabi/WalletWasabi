using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Backend.Models.Requests;

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

		public static int CalculateVsizeAssumeSegwit(int inNum, int outNum)
		{
			var origTxSize = inNum * Constants.P2pkhInputSizeInBytes + outNum * Constants.OutputSizeInBytes + 10;
			var newTxSize = inNum * Constants.P2wpkhInputSizeInBytes + outNum * Constants.OutputSizeInBytes + 10; // BEWARE: This assumes segwit only inputs!
			var vSize = (int)Math.Ceiling(((3 * newTxSize) + origTxSize) / 4m);
			return vSize;
		}

		public static byte[] SignData(this ECDSABlinding.Signer signer, byte[] data )
		{
			if (data.Length != 32)
				throw new ArgumentException("Invalid data lenght for a blinded message", nameof(data));
			BigInteger signature = signer.Sign(new uint256(data));
			return signature.ToByteArray();
		}

		public static bool VerifySignature(this ECDSABlinding.Signer signer, byte[] data, BlindSignature signature)
		{
			uint256 hash = new uint256(Hashes.SHA256(data));
			return ECDSABlinding.VerifySignature(hash, signature, signer.Key.PubKey);
		}

		public static BlindSignature UnblindSignature(this ECDSABlinding.Requester requester, byte[] blindedSignature)
		{
			BigInteger blinded = new BigInteger(blindedSignature);
			return requester.UnblindSignature(blinded);
		}

		public static WrappedBlindSignature Wrap(this BlindSignature signature)
		{
			return WrappedBlindSignature.Wrap(signature);
		}
	}
}
