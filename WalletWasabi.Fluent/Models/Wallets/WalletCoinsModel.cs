using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

#pragma warning disable CA2000

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class WalletCoinsModel : IDisposable
{
	private readonly Wallet _wallet;
	private readonly IWalletModel _walletModel;
	private readonly ReadOnlyObservableCollection<ICoinModel> _coins;
	private readonly ReadOnlyObservableCollection<Pocket> _pockets;
	private readonly CompositeDisposable _disposables = new();

	public WalletCoinsModel(Wallet wallet, IWalletModel walletModel)
	{
		_wallet = wallet;
		_walletModel = walletModel;
		var transactionProcessed = walletModel.Transactions.TransactionProcessed;
		var anonScoreTargetChanged = walletModel.WhenAnyValue(x => x.Settings.AnonScoreTarget).Skip(1).ToSignal();
		var isCoinjoinRunningChanged = walletModel.Coinjoin.IsRunning.ToSignal();

		var signals = transactionProcessed
			.Merge(anonScoreTargetChanged)
			.Merge(isCoinjoinRunningChanged)
			.Publish();

		var coinRetriever = new SignaledFetcher<ICoinModel, int>(signals, x => x.Key, GetCoins)
			.DisposeWith(_disposables);

		coinRetriever.Changes
			.Bind(out _coins)
			.Subscribe()
			.DisposeWith(_disposables);

		var pocketRetriever = new SignaledFetcher<Pocket, LabelsArray>(signals, x => x.Labels, GetPockets);

		pocketRetriever.Changes
			.Bind(out _pockets)
			.Subscribe()
			.DisposeWith(_disposables);

		signals
			.Do(_ => Logger.LogDebug($"Refresh signal emitted in {walletModel.Name}"))
			.Subscribe()
			.DisposeWith(_disposables);

		signals.Connect();
	}

	public ReadOnlyObservableCollection<ICoinModel> List => _coins;
	public ReadOnlyObservableCollection<Pocket> Pockets => _pockets;

	public List<ICoinModel> GetSpentCoins(BuildTransactionResult? transaction)
	{
		var coins = (transaction?.SpentCoins ?? new List<SmartCoin>()).ToList();
		return coins.Select(GetCoinModel).ToList();
	}

	public ICoinModel GetCoinModel(SmartCoin smartCoin)
	{
		return new CoinModel(smartCoin, _walletModel.Settings.AnonScoreTarget);
	}

	public bool AreEnoughToCreateTransaction(TransactionInfo transactionInfo, IEnumerable<ICoinModel> coins)
	{
		return TransactionHelpers.TryBuildTransactionWithoutPrevTx(_wallet.KeyManager, transactionInfo, _wallet.Coins, coins.GetSmartCoins(), _wallet.Kitchen.SaltSoup(), out _);
	}

	private Pocket[] GetPockets()
	{
		return _wallet.GetPockets().ToArray();
	}

	private ICoinModel[] GetCoins()
	{
		return _wallet.Coins.Select(GetCoinModel).ToArray();
	}

	public void Dispose() => _disposables.Dispose();
}
