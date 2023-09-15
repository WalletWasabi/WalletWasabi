using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class WalletCoinsModel
{
	private readonly Wallet _wallet;

	public WalletCoinsModel(Wallet wallet, IWalletModel walletModel)
	{
		_wallet = wallet;

		var initialCoinList = Observable.Defer(() => GetCoins().ToObservable());
		var initialPocketList = Observable.Defer(() => _wallet.GetPockets().ToObservable());
		var transactionProcessed = walletModel.Transactions.TransactionProcessed;
		var anonScoreTargetChanged = walletModel.WhenAnyValue(x => x.Settings.AnonScoreTarget).ToSignal();
		var isCoinjoinRunningChanged = Observable.Defer(() => walletModel.Coinjoin.IsRunning.ToSignal());
		var signals = transactionProcessed.Merge(anonScoreTargetChanged).Merge(isCoinjoinRunningChanged);

		List =
			initialCoinList
				.Concat(signals.SelectMany(_ => GetCoins()))
				.ToObservableChangeSet(x => x.Key)
				.ObserveOn(RxApp.MainThreadScheduler);

		Pockets =
			initialPocketList
				.Concat(signals.SelectMany(_ => _wallet.GetPockets().ToObservable()))
				.ToObservableChangeSet(x => x.Labels)
				.ObserveOn(RxApp.MainThreadScheduler);
	}

	public IObservable<IChangeSet<ICoinModel, int>> List { get; }

	public IObservable<IChangeSet<Pocket, LabelsArray>> Pockets { get; }

	private IEnumerable<ICoinModel> GetCoins()
	{
		return _wallet.Coins.Select(x => new CoinModel(_wallet, x));
	}
}
