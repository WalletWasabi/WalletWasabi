using System.Linq;
using System.Reactive;
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
	private readonly IObservable<Unit> _signals;

	public WalletCoinsModel(Wallet wallet, IWalletModel walletModel)
	{
		_wallet = wallet;

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

	private Pocket[] GetPockets()
	{
		return _wallet.GetPockets().ToArray();
	}

	private ICoinModel[] GetCoins()
	{
		return _wallet.Coins.Select(x => new CoinModel(_wallet, x)).ToArray();
	}
}
