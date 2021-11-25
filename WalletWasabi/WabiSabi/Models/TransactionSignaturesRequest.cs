using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.WabiSabi.Models
{
	public record TransactionSignaturesRequest(uint256 RoundId, IEnumerable<InputWitnessPair> InputWitnessPairs)
	{
		public override int GetHashCode()
		{
			int hash = 0;

			foreach (InputWitnessPair item in InputWitnessPairs)
			{
				hash = (hash << 4) + item.GetHashCode();
			}

			return HashCode.Combine(RoundId, hash);
		}

		public virtual bool Equals(TransactionSignaturesRequest? other)
		{
			if (other is null)
			{
				return false;
			}

			bool isEqual = RoundId == other.RoundId
				&& InputWitnessPairs.SequenceEqual(other.InputWitnessPairs);

			return isEqual;
		}
	}
}
