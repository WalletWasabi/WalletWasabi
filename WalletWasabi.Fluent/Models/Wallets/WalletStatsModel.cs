using ReactiveUI;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial interface IWalletStatsModel : IDisposable
{
}

[AutoInterface]
public partial class WalletStatsModel : ReactiveObject, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	[AutoNotify] private int _coinCount;
	[AutoNotify] private Amount _balance;
	[AutoNotify] private Amount _confirmedBalance;
	[AutoNotify] private Amount _unconfirmedBalance;
	[AutoNotify] private int _generatedKeyCount;
	[AutoNotify] private int _generatedCleanKeyCount;
	[AutoNotify] private int _generatedUsedKeyCount;
	[AutoNotify] private int _totalTransactionCount;
	[AutoNotify] private int _nonCoinjointransactionCount;
	[AutoNotify] private int _coinjoinTransactionCount;

	public WalletStatsModel(IWalletModel walletModel, Wallet wallet)
	{
		_balance = Amount.Zero;
		_confirmedBalance = Amount.Zero;
		_unconfirmedBalance = Amount.Zero;

		walletModel.Transactions.TransactionProcessed
								.Do(_ => Update(walletModel, wallet))
								.Subscribe()
								.DisposeWith(_disposables);
	}

	public void Dispose()
	{
		_disposables.Dispose();
	}

	private void Update(IWalletModel walletModel, Wallet wallet)
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
		GeneratedUsedKeyCount = wallet.KeyManager.GetKeys(KeyState.Used).Count();

		var singleCoinjoins =
			walletModel.Transactions.Cache.Items
									.Where(x => x.Type == TransactionType.Coinjoin)
									.ToList();

		var groupedCoinjoins =
			walletModel.Transactions.Cache.Items
									.Where(x => x.Type == TransactionType.CoinjoinGroup)
									.ToList();

		var nestedCoinjoins = groupedCoinjoins.SelectMany(x => x.Children).ToList();
		var nonCoinjoins =
			walletModel.Transactions.Cache.Items
									.Where(x => !x.IsCoinjoin)
									.ToList();

		TotalTransactionCount = singleCoinjoins.Count + nestedCoinjoins.Count + nonCoinjoins.Count;
		NonCoinjointransactionCount = nonCoinjoins.Count;
		CoinjoinTransactionCount = singleCoinjoins.Count + nestedCoinjoins.Count;
	}
}
