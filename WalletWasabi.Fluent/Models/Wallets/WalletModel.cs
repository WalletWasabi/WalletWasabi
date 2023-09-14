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
	private readonly Lazy<IWalletCoinjoinModel> _coinjoin;

	public WalletModel(Wallet wallet)
	{
		Wallet = wallet;

		Auth = new WalletAuthModel(this, Wallet);
		Loader = new WalletLoadWorkflow(Wallet);
		Settings = new WalletSettingsModel(Wallet.KeyManager);

		_coinjoin = new(() => new WalletCoinjoinModel(Wallet, Settings));

		var relevantTransactionProcessed =
			Observable.FromEventPattern<ProcessedResult?>(Wallet, nameof(Wallet.WalletRelevantTransactionProcessed)).ToSignal()
					  .Merge(Observable.FromEventPattern(Wallet, nameof(Wallet.NewFiltersProcessed)).ToSignal())
					  .Sample(TimeSpan.FromSeconds(1))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .StartWith(Unit.Default);

		Coins =
			Observable.Defer(() => GetCoins().ToObservable())                                                 // initial coin list
					  .Concat(relevantTransactionProcessed.SelectMany(_ => GetCoins()))                       // Refresh whenever there's a relevant transaction
					  .Concat(this.WhenAnyValue(x => x.Settings.AnonScoreTarget).SelectMany(_ => GetCoins())) // Also refresh whenever AnonScoreTarget changes
					  .ToObservableChangeSet();

		Transactions =
			Observable.Defer(() => GetTransactions().ToObservable())
					  .Concat(relevantTransactionProcessed.SelectMany(_ => GetTransactions()))
					  .ToObservableChangeSet(x => x.GetHash());

		Addresses =
			Observable.Defer(() => GetAddresses().ToObservable())
					  .Concat(relevantTransactionProcessed.ToSignal().SelectMany(_ => GetAddresses()))
					  .ToObservableChangeSet(x => x.Text);

		State =
			Observable.FromEventPattern<WalletState>(Wallet, nameof(Wallet.StateChanged))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .Select(_ => Wallet.State);

		Privacy = new WalletPrivacyModel(this, Wallet);

		var balance =
			Observable.Defer(() => Observable.Return(Wallet.Coins.TotalAmount()))
					  .Concat(relevantTransactionProcessed.Select(_ => Wallet.Coins.TotalAmount()));
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

	internal Wallet Wallet { get; }

	public IWalletBalancesModel Balances { get; }

	public IWalletAuthModel Auth { get; }

	public IWalletLoadWorkflow Loader { get; }

	public IWalletSettingsModel Settings { get; }

	public IWalletPrivacyModel Privacy { get; }

	public IWalletCoinjoinModel Coinjoin => _coinjoin.Value;

	public IObservable<IChangeSet<ICoinModel>> Coins { get; }

	public IObservable<IChangeSet<IAddress, string>> Addresses { get; }

	public string Name => Wallet.WalletName;

	public IObservable<WalletState> State { get; }

	public IObservable<IChangeSet<SmartTransaction, uint256>> Transactions { get; }

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

	private IEnumerable<ICoinModel> GetCoins()
	{
		return Wallet.Coins.Select(x => new CoinModel(Wallet, x));
	}

	private IEnumerable<SmartTransaction> GetTransactions()
	{
		return Wallet.GetTransactions();
	}

	private IEnumerable<IAddress> GetAddresses()
	{
		return Wallet.KeyManager
			.GetKeys()
			.Reverse()
			.Select(x => new Address(Wallet.KeyManager, x));
	}
}
