using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class WalletCoinsModel
{
	private readonly Wallet _wallet;
	private readonly IWalletModel _walletModel;
	private readonly IObservable<Unit> _signals;

	public WalletCoinsModel(Wallet wallet, IWalletModel walletModel)
	{
		_wallet = wallet;
		_walletModel = walletModel;
		var transactionProcessed = walletModel.Transactions.TransactionProcessed;
		var anonScoreTargetChanged = walletModel.WhenAnyValue(x => x.Settings.AnonScoreTarget).ToSignal();
		var isCoinjoinRunningChanged = walletModel.Coinjoin.IsRunning.ToSignal();

		_signals =
			transactionProcessed
				.Merge(anonScoreTargetChanged)
				.Merge(isCoinjoinRunningChanged)
				.StartWith(Unit.Default);
	}

	public IObservable<IChangeSet<ICoinModel, int>> List => _signals.ProjectList(GetCoins, x => x.Key);

	public IObservable<IChangeSet<Pocket, LabelsArray>> Pockets => _signals.ProjectList(GetPockets, x => x.Labels);

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
}
