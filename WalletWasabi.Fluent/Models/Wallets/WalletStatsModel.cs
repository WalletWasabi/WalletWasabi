using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class WalletStatsModel
{
	public WalletStatsModel(IWalletModel walletModel, Wallet wallet)
	{
		// Number of coins in the wallet.
		CoinCount = wallet.Coins.Unspent().Count();

		// Total amount of money in the wallet.
		Balance = walletModel.AmountProvider.Create(wallet.Coins.TotalAmount());

		// Total amount of confirmed money in the wallet.
		ConfirmedBalance = walletModel.AmountProvider.Create(wallet.Coins.Confirmed().TotalAmount());

		// Total amount of unconfirmed money in the wallet.
		UnconfirmedBalance = walletModel.AmountProvider.Create(wallet.Coins.Unconfirmed().TotalAmount());

		GeneratedKeyCount = wallet.KeyManager.GetKeys().Count();
		GeneratedCleanKeyCount = wallet.KeyManager.GetKeys(KeyState.Clean).Count();
		GeneratedLockedKeyCount = wallet.KeyManager.GetKeys(KeyState.Locked).Count();
		GeneratedUsedKeyCount = wallet.KeyManager.GetKeys(KeyState.Used).Count();

		var singleCoinjoins =
			walletModel.Transactions.List
									.Where(x => x.Type == TransactionType.Coinjoin)
									.ToList();

		var groupedCoinjoins =
			walletModel.Transactions.List
									.Where(x => x.Type == TransactionType.CoinjoinGroup)
									.ToList();

		var nestedCoinjoins = groupedCoinjoins.SelectMany(x => x.Children).ToList();
		var nonCoinjoins =
			walletModel.Transactions
					   .Where(x => x.Type != )
					   .ToList();

		TotalTransactionCount = singleCoinjoins.Count + nestedCoinjoins.Count + nonCoinjoins.Count;
		NonCoinjointransactionCount = nonCoinjoins.Count;
		CoinjoinTransactionCount = singleCoinjoins.Count + nestedCoinjoins.Count;
	}

	public int CoinCount { get; }

	public Amount Balance { get; }

	public Amount ConfirmedBalance { get; }

	public Amount UnconfirmedBalance { get; }
}
