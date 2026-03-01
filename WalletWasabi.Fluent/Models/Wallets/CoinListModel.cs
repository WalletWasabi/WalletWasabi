using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface ICoinListModel
{
	IObservableCache<CoinModel, int> List { get; }

	IObservableCache<Pocket, LabelsArray> Pockets { get; }

	CoinModel GetCoinModel(SmartCoin smartCoin);

	/* SKIPPED: Implements an interface */ /* void Dispose(); */
}

public abstract partial class CoinListModel : ICoinListModel, IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	public CoinListModel(Wallet wallet, IWalletModel walletModel)
	{
		Wallet = wallet;
		WalletModel = walletModel;
		var transactionProcessed = walletModel.Transactions.TransactionProcessed;
		var anonScoreTargetChanged = this.WhenAnyValue(x => x.WalletModel.Settings.AnonScoreTarget).Skip(1).ToSignal();
		var isCoinjoinRunningChanged = walletModel.IsCoinjoinRunning.ToSignal();
		var isSelected = this.WhenAnyValue(x => x.WalletModel.IsSelected).Skip(1).ToSignal();

		var signals =
			transactionProcessed
				.Merge(anonScoreTargetChanged)
				.Merge(isCoinjoinRunningChanged)
				.Merge(isSelected)
				.Publish();

		List = signals.Fetch(CreateCoinModels, x => x.Key).DisposeWith(_disposables);
		Pockets = signals.Fetch(GetPockets, x => x.Labels).DisposeWith(_disposables);

		signals
			.Do(_ => Logger.LogDebug($"Refresh signal emitted in {walletModel.Name}"))
			.Subscribe()
			.DisposeWith(_disposables);

		signals.Connect()
			.DisposeWith(_disposables);
	}

	protected Wallet Wallet { get; }
	protected IWalletModel WalletModel { get; }

	public IObservableCache<CoinModel, int> List { get; }

	public IObservableCache<Pocket, LabelsArray> Pockets { get; }

	public CoinModel GetCoinModel(SmartCoin smartCoin)
	{
		return List.Items.First(coinModel => coinModel.Key == smartCoin.Outpoint.GetHashCode());
	}

	protected CoinModel CreateCoinModel(SmartCoin smartCoin)
	{
		return new CoinModel(smartCoin, WalletModel.Network, WalletModel.Settings.AnonScoreTarget);
	}

	protected abstract Pocket[] GetPockets();

	protected abstract CoinModel[] CreateCoinModels();

	public void Dispose() => _disposables.Dispose();
}
