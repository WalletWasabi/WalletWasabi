using NBitcoin;
using WalletWasabi.CoinJoin.Coordinator.MixingLevels;
using WalletWasabi.Helpers;

namespace WalletWasabi.CoinJoin.Coordinator.Participants
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
