using NBitcoin;
using NBitcoin.Crypto;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models.ChaumianCoinJoin
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
