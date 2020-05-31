using NBitcoin;
using Newtonsoft.Json;
using System;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin.Common.Models
{
	public class BlindedOutputWithNonceIndex : IEquatable<BlindedOutputWithNonceIndex>
	{
		public BlindedOutputWithNonceIndex(int n, uint256 blindedOutput)
		{
			N = n;
			BlindedOutput = blindedOutput;
		}

		public int N { get; set; }

		[JsonConverter(typeof(Uint256JsonConverter))]
		public uint256 BlindedOutput { get; set; }

		public override bool Equals(object obj)
		{
			if (obj is BlindedOutputWithNonceIndex other)
			{
				return Equals(other);
			}
			return false;
		}

		public bool Equals(BlindedOutputWithNonceIndex other)
		{
			return other?.BlindedOutput == BlindedOutput;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(BlindedOutput, N);
		}
	}
}
