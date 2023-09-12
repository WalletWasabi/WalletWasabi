using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class WalletModel : ReactiveObject
{
	private readonly TransactionHistoryBuilder _historyBuilder;
	private readonly Lazy<IWalletCoinjoinModel> _coinjoin;

	public WalletModel(Wallet wallet)
	{
		Wallet = wallet;

		_historyBuilder = new TransactionHistoryBuilder(Wallet);

		Auth = new WalletAuthModel(this, Wallet);
		Loader = new WalletLoadWorkflow(Wallet);
		Settings = new WalletSettingsModel(Wallet.KeyManager);

		_coinjoin = new(() => new WalletCoinjoinModel(Wallet, Settings));

		TransactionProcessed =
			Observable.FromEventPattern<ProcessedResult?>(Wallet, nameof(Wallet.WalletRelevantTransactionProcessed)).ToSignal()
					  .Merge(Observable.FromEventPattern(Wallet, nameof(Wallet.NewFiltersProcessed)).ToSignal())
					  .Sample(TimeSpan.FromSeconds(1))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .StartWith(Unit.Default);

		Transactions =
			Observable.Defer(() => BuildSummary().ToObservable())
					  .Concat(TransactionProcessed.SelectMany(_ => BuildSummary()))
					  .ToObservableChangeSet(x => x.TransactionId);

		Addresses =
			Observable.Defer(() => GetAddresses().ToObservable())
					  .Concat(TransactionProcessed.ToSignal().SelectMany(_ => GetAddresses()))
					  .ToObservableChangeSet(x => x.Text);

		State =
			Observable.FromEventPattern<WalletState>(Wallet, nameof(Wallet.StateChanged))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .Select(_ => Wallet.State);

		Privacy = new WalletPrivacyModel(this, Wallet);

		var balance =
			Observable.Defer(() => Observable.Return(Wallet.Coins.TotalAmount()))
					  .Concat(TransactionProcessed.Select(_ => Wallet.Coins.TotalAmount()));
		Balances = new WalletBalancesModel(balance, new ExchangeRateProvider(wallet.Synchronizer));

		Coins = new WalletCoinsModel(wallet, this);

		// Start the Loader after wallet is logged in
		this.WhenAnyValue(x => x.Auth.IsLoggedIn)
			.Where(x => x)
			.Take(1)
			.Do(_ => Loader.Start())
			.Subscribe();

		// Stop the loader after load is completed
		State.Where(x => x == WalletState.Started)
			 .Do(_ => Loader.Stop())
			 .Subscribe();
	}

	internal Wallet Wallet { get; }

	public IWalletBalancesModel Balances { get; }

	public IWalletCoinsModel Coins { get; }

	public IWalletAuthModel Auth { get; }

	public IWalletLoadWorkflow Loader { get; }

	public IWalletSettingsModel Settings { get; }

	public IWalletPrivacyModel Privacy { get; }

	public IWalletCoinjoinModel Coinjoin => _coinjoin.Value;

	public IObservable<Unit> TransactionProcessed { get; }

	public IObservable<IChangeSet<IAddress, string>> Addresses { get; }

	public string Name => Wallet.WalletName;

	public IObservable<WalletState> State { get; }

	public IObservable<IChangeSet<TransactionSummary, uint256>> Transactions { get; }

	public IAddress GetNextReceiveAddress(IEnumerable<string> destinationLabels)
	{
		var pubKey = Wallet.GetNextReceiveAddress(destinationLabels);
		return new Address(Wallet.KeyManager, pubKey);
	}

	public IWalletInfoModel GetWalletInfo()
	{
		return new WalletInfoModel(Wallet);
	}

	public bool IsHardwareWallet => Wallet.KeyManager.IsHardwareWallet;

	public bool IsWatchOnlyWallet => Wallet.KeyManager.IsWatchOnly;

	public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent)
	{
		return Wallet.GetLabelsWithRanking(intent);
	}

	private IEnumerable<TransactionSummary> BuildSummary()
	{
		return _historyBuilder.BuildHistorySummary();
	}

	private IEnumerable<IAddress> GetAddresses()
	{
		return Wallet.KeyManager
			.GetKeys()
			.Reverse()
			.Select(x => new Address(Wallet.KeyManager, x));
	}
}

[AutoInterface]
public partial class WalletCoinsModel
{
	private readonly Wallet _wallet;

	public WalletCoinsModel(Wallet wallet, IWalletModel walletModel)
	{
		_wallet = wallet;

		List =
			Observable.Defer(() => GetCoins().ToObservable())                                                        // initial coin list
					  .Concat(walletModel.TransactionProcessed.SelectMany(_ => GetCoins()))                          // Refresh whenever there's a relevant transaction
					  .Concat(walletModel.WhenAnyValue(x => x.Settings.AnonScoreTarget).SelectMany(_ => GetCoins())) // Also refresh whenever AnonScoreTarget changes
					  .ToObservableChangeSet();

		Pockets =
			Observable.Defer(() => _wallet.GetPockets().ToObservable())                                                       // initial pocket list
					  .Concat(walletModel.TransactionProcessed.SelectMany(_ => wallet.GetPockets()))                          // Refresh whenever there's a relevant transaction
					  .Concat(walletModel.WhenAnyValue(x => x.Settings.AnonScoreTarget).SelectMany(_ => wallet.GetPockets())) // Also refresh whenever AnonScoreTarget changes
					  .ToObservableChangeSet();
	}

	public IObservable<IChangeSet<ICoinModel>> List { get; }

	public IObservable<IChangeSet<Pocket>> Pockets { get; }

	private IEnumerable<ICoinModel> GetCoins()
	{
		return _wallet.Coins.Select(x => new CoinModel(_wallet, x));
	}
}
