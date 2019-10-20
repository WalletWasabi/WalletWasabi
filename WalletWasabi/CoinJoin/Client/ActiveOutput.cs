using NBitcoin;
using NBitcoin.Crypto;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.CoinJoin.Client
{
	public class ActiveOutput
	{
		public BitcoinAddress Address { get; }
		public UnblindedSignature Signature { get; }
		public int MixingLevel { get; }

		public ActiveOutput(BitcoinAddress address, UnblindedSignature signature, int mixingLevel)
		{
			Address = Guard.NotNull(nameof(address), address);
			Signature = Guard.NotNull(nameof(signature), signature);
			MixingLevel = Guard.MinimumAndNotNull(nameof(mixingLevel), mixingLevel, 0);
		}
	}
}
