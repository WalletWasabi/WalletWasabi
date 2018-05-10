using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
    public class Bob
    {
		public Script ActiveOutputScript { get; }

		public Bob(Script activeOutputScript)
		{
			Guard.NotNull(nameof(activeOutputScript), activeOutputScript);
			// 33 bytes maximum: https://bitcoin.stackexchange.com/a/46379/26859
			int byteCount = activeOutputScript.ToBytes().Length;
			if (byteCount > 33)
			{
				throw new ArgumentOutOfRangeException(nameof(activeOutputScript), byteCount, $"Can be maximum 33 bytes.");
			}
			ActiveOutputScript = activeOutputScript;
		}
	}
}
