using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	public class Bob
	{
		public MixingLevel Level { get; }
		public BitcoinAddress ActiveOutputAddress { get; }

		public Bob(BitcoinAddress activeOutputAddress, MixingLevel level)
		{
			ActiveOutputAddress = Guard.NotNull(nameof(activeOutputAddress), activeOutputAddress);
			Level = Guard.NotNull(nameof(level), level);
		}
	}
}
