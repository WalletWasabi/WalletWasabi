using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models
{
	public class MixCoin
	{
		public SmartCoin SmartCoin { get; }
		public ISecret Secret { get; }
		public long? RoundId { get; private set; }

		public MixCoin(SmartCoin coin, ISecret secret)
		{
			SmartCoin = Guard.NotNull(nameof(coin), coin);
			if(!SmartCoin.Locked)
			{
				throw new NotSupportedException("Lock SmartCoin before creating a MixCoin.");
			}
			Secret = Guard.NotNull(nameof(secret), secret);
		}

		public bool IsMixing() => RoundId != null;
		public void RemoveFromMix() => RoundId = null;
		public void AddToMix(long roundId) => RoundId = roundId;
	}
}
