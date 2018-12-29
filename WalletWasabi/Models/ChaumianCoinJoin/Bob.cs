using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	public class Bob
	{
		public int Level { get; }
		public BitcoinAddress ActiveOutputAddress { get; }

		public Bob(BitcoinAddress activeOutputAddress, int level)
		{
			ActiveOutputAddress = Guard.NotNull(nameof(activeOutputAddress), activeOutputAddress);
			Level = Guard.MinimumAndNotNull(nameof(level), level, 0);
		}
	}
}
