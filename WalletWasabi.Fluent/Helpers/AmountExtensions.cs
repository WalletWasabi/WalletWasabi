using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.Helpers;

public static class AmountExtensions
{
	public static decimal Diff(this Amount self, Amount toCompare)
	{
		return ((decimal)self.Btc.Satoshi / toCompare.Btc.Satoshi) - 1;
	}
}
