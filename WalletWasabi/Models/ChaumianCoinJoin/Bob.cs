using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	public class Bob
	{
		public BitcoinAddress ActiveOutputAddress { get; }

		public Bob(BitcoinAddress activeOutputAddress)
		{
			ActiveOutputAddress = Guard.NotNull(nameof(activeOutputAddress), activeOutputAddress);
		}
	}
}
