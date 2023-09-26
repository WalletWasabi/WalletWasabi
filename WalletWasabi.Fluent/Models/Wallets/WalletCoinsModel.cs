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

	public WalletCoinsModel(Wallet wallet, IWalletModel walletModel)
	{
		_wallet = wallet;

		var transactionProcessed = walletModel.TransactionProcessed;
		var anonScoreTargetChanged = walletModel.WhenAnyValue(x => x.Settings.AnonScoreTarget).ToSignal();
		var signals =
			transactionProcessed.Merge(anonScoreTargetChanged)
				.StartWith(Unit.Default);

		List = signals.ProjectList(GetCoins, x => x.Key);
		Pockets = signals.ProjectList(GetPockets, x => x.Labels);
	}

	public IObservable<IChangeSet<ICoinModel, int>> List { get; }

	public IObservable<IChangeSet<Pocket, LabelsArray>> Pockets { get; }

	private Pocket[] GetPockets()
	{
		return _wallet.GetPockets().ToArray();
	}

	private ICoinModel[] GetCoins()
	{
		return _wallet.Coins.Select(x => new CoinModel(_wallet, x)).ToArray();
	}
}
