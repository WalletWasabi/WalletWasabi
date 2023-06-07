using DynamicData;

namespace WalletWasabi.Fluent.Models.Wallets;

public static class WalletModelExtensions
{
	public static IObservable<IChangeSet<IAddress, string>> UnusedAddresses(this IWalletModel wallet) =>
		wallet.Addresses
			.AutoRefresh(x => x.IsUsed)
			.Filter(x => !x.IsUsed);
}
