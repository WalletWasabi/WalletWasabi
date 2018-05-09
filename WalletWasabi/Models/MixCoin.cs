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

		public MixCoin(SmartCoin coin, ISecret secret)
		{
			SmartCoin = Guard.NotNull(nameof(coin), coin);
			if(!SmartCoin.Locked)
			{
				throw new NotSupportedException("Lock SmartCoin before creating a MixCoin.");
			}
			Secret = Guard.NotNull(nameof(secret), secret);
		}
	}
}
