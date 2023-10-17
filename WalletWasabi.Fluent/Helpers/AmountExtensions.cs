using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.Helpers;

public static class AmountExtensions
{
	public static double? Diff(this Amount? self, Amount? toCompare)
	{
		if (self is null || toCompare is null)
		{
			return null;
		}

		return (double) self.Btc.Satoshi / toCompare.Btc.Satoshi - 1;
	}
}
