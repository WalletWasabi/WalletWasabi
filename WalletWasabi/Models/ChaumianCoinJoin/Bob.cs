using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	public class Bob
	{
		public BitcoinAddress ActiveOutputAddress { get; }

		public Bob(BitcoinAddress activeOutputAddress)
		{
			Guard.NotNull(nameof(activeOutputAddress), activeOutputAddress);
			// 33 bytes maximum: https://bitcoin.stackexchange.com/a/46379/26859
			int byteCount = activeOutputAddress.ScriptPubKey.ToBytes().Length;
			if (byteCount > 33)
			{
				throw new ArgumentOutOfRangeException(nameof(activeOutputAddress), byteCount, $"Can be maximum 33 bytes.");
			}
			ActiveOutputAddress = activeOutputAddress;
		}
	}
}
