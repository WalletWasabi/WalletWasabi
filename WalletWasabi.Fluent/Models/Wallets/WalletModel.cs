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

public partial class WalletModel : ReactiveObject, IWalletModel
{
	private readonly Wallet _wallet;
	private readonly TransactionHistoryBuilder _historyBuilder;

	[AutoNotify] private bool _isLoggedIn;

	public WalletModel(Wallet wallet)
	{
		_wallet = wallet;

		_historyBuilder = new TransactionHistoryBuilder(_wallet);

		Auth = new WalletAuthModel(this, _wallet);
		Loader = new WalletLoadWorkflow(_wallet);
		Settings = new WalletSettingsModel(_wallet.KeyManager);

		RelevantTransactionProcessed =
			Observable.FromEventPattern<ProcessedResult?>(_wallet, nameof(_wallet.WalletRelevantTransactionProcessed))
					  .ObserveOn(RxApp.MainThreadScheduler);

		Coins =
			Observable.Defer(() => GetCoins().ToObservable())                                                 // initial coin list
					  .Concat(RelevantTransactionProcessed.SelectMany(_ => GetCoins()))                       // Refresh whenever there's a relevant transaction
					  .Concat(this.WhenAnyValue(x => x.Settings.AnonScoreTarget).SelectMany(_ => GetCoins())) // Also refresh whenever AnonScoreTarget changes
					  .ToObservableChangeSet();

		Transactions = Observable
			.Defer(() => BuildSummary().ToObservable())
			.Concat(RelevantTransactionProcessed.SelectMany(_ => BuildSummary()))
			.ToObservableChangeSet(x => x.TransactionId);

		Addresses = Observable
			.Defer(() => GetAddresses().ToObservable())
			.Concat(RelevantTransactionProcessed.ToSignal().SelectMany(_ => GetAddresses()))
			.ToObservableChangeSet(x => x.Text);

		State = Observable.FromEventPattern<WalletState>(_wallet, nameof(Wallet.StateChanged))
						  .ObserveOn(RxApp.MainThreadScheduler)
						  .Select(_ => _wallet.State);

		var balance = Observable
			.Defer(() => Observable.Return(_wallet.Coins.TotalAmount()))
			.Concat(RelevantTransactionProcessed.Select(_ => _wallet.Coins.TotalAmount()));
		Balances = new WalletBalancesModel(balance, new ExchangeRateProvider(wallet.Synchronizer));

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

	// TODO: Remove this
	public Wallet Wallet => _wallet;

	public IWalletBalancesModel Balances { get; }

	public IWalletAuthModel Auth { get; }

	public IWalletLoadWorkflow Loader { get; }

	public IWalletSettingsModel Settings { get; }

	public IObservable<IChangeSet<ICoinModel>> Coins { get; }

	public IObservable<IChangeSet<IAddress, string>> Addresses { get; }

	private IObservable<EventPattern<ProcessedResult?>> RelevantTransactionProcessed { get; }

	public string Name => _wallet.WalletName;

	public IObservable<WalletState> State { get; }

	public IObservable<IChangeSet<TransactionSummary, uint256>> Transactions { get; }

	public IAddress GetNextReceiveAddress(IEnumerable<string> destinationLabels)
	{
		var pubKey = _wallet.GetNextReceiveAddress(destinationLabels);
		return new Address(_wallet.KeyManager, pubKey);
	}

	public bool IsHardwareWallet => _wallet.KeyManager.IsHardwareWallet;

	public bool IsWatchOnlyWallet => _wallet.KeyManager.IsWatchOnly;

	public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent)
	{
		return _wallet.GetLabelsWithRanking(intent);
	}

	private IEnumerable<ICoinModel> GetCoins()
	{
		return _wallet.Coins
					  .Select(x => new CoinModel(_wallet, x));
	}

	private IEnumerable<TransactionSummary> BuildSummary()
	{
		return _historyBuilder.BuildHistorySummary();
	}

	private IEnumerable<IAddress> GetAddresses()
	{
		return _wallet.KeyManager
			.GetKeys()
			.Reverse()
			.Select(x => new Address(_wallet.KeyManager, x));
	}
}
