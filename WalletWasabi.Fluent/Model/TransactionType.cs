using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Model
{
	public enum TransactionType
	{
		Incoming,
		Outgoing,
		[FriendlyName("Self Spend")]
		SelfSpend,
		CoinJoin
	}
}
