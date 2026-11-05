using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletStatsModel : IDisposable
{
	int CoinCount { get; set; }

	Amount Balance { get; set; }

	Amount ConfirmedBalance { get; set; }

	Amount UnconfirmedBalance { get; set; }

	int GeneratedKeyCount { get; set; }

	int GeneratedCleanKeyCount { get; set; }

	int GeneratedUsedKeyCount { get; set; }

	int TotalTransactionCount { get; set; }

	int NonCoinjointransactionCount { get; set; }

	int CoinjoinTransactionCount { get; set; }

	uint BirthHeight { get; set; }
}

public partial class WalletStatsModel : ReactiveObject, IWalletStatsModel, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private readonly IWalletModel _walletModel;
	private readonly Wallet _wallet; // Internal access for complex operations

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
	[AutoNotify] private uint _birthHeight;

	public WalletStatsModel(IWalletModel walletModel)
	{
		_walletModel = walletModel;
		// Internal access to Wallet through WalletModel for model-level operations
		_wallet = ((WalletModel)walletModel).Wallet;

		_balance = Amount.Zero;
		_confirmedBalance = Amount.Zero;
		_unconfirmedBalance = Amount.Zero;
		_birthHeight = _wallet.KeyManager.GetBirthHeight() is { } h ? h.Height : 0u;

		walletModel.Transactions.TransactionProcessed
								.Do(_ => Update())
								.Subscribe()
								.DisposeWith(_disposables);
	}

	public void Dispose()
	{
		_disposables.Dispose();
	}

	private void Update()
	{
		// Number of coins in the wallet.
		CoinCount = _wallet.Coins.Unspent().Count();

		// Total amount of money in the wallet.
		Balance = _walletModel.AmountProvider.Create(_wallet.Coins.TotalAmount());

		// Total amount of confirmed money in the wallet.
		ConfirmedBalance = _walletModel.AmountProvider.Create(_wallet.Coins.Confirmed().TotalAmount());

		// Total amount of unconfirmed money in the wallet.
		UnconfirmedBalance = _walletModel.AmountProvider.Create(_wallet.Coins.Unconfirmed().TotalAmount());

		GeneratedKeyCount = _wallet.KeyManager.GetKeys().Length;
		GeneratedCleanKeyCount = _wallet.KeyManager.GetKeys(KeyState.Clean).Length;
		GeneratedUsedKeyCount = _wallet.KeyManager.GetKeys(KeyState.Used).Length;

		var singleCoinjoins =
			_walletModel.Transactions.Cache.Items
									.OfType<CoinJoinTransactionModel>()
									.ToList();

		var groupedCoinjoins =
			_walletModel.Transactions.Cache.Items
									.OfType<CoinJoinTransactionGroupModel>()
									.ToList();

		var nestedCoinjoins = groupedCoinjoins.SelectMany(x => x.Children).ToList();
		var nonCoinjoins =
			_walletModel.Transactions.Cache.Items
									.Where(x => x is not (CoinJoinTransactionModel or CoinJoinTransactionGroupModel))
									.ToList();

		TotalTransactionCount = singleCoinjoins.Count + nestedCoinjoins.Count + nonCoinjoins.Count;
		NonCoinjointransactionCount = nonCoinjoins.Count;
		CoinjoinTransactionCount = singleCoinjoins.Count + nestedCoinjoins.Count;
	}
}
