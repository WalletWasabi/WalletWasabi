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

		RelevantTransactionProcessed = Observable
			.FromEventPattern<ProcessedResult?>(_wallet, nameof(_wallet.WalletRelevantTransactionProcessed))
			.ObserveOn(RxApp.MainThreadScheduler);

		Transactions = Observable
			.Defer(() => BuildSummary().ToObservable())
			.Concat(RelevantTransactionProcessed.SelectMany(_ => BuildSummary()))
			.ToObservableChangeSet(x => x.TransactionId);

		Addresses = Observable
			.Defer(() => GetAddresses().ToObservable())
			.Concat(RelevantTransactionProcessed.ToSignal().SelectMany(_ => GetAddresses()))
			.ToObservableChangeSet(x => x.Text);

		WalletType = WalletHelpers.GetType(_wallet.KeyManager);

		State = Observable.FromEventPattern<WalletState>(_wallet, nameof(Wallet.StateChanged))
						  .ObserveOn(RxApp.MainThreadScheduler)
						  .Select(_ => _wallet.State);

		Auth = new WalletAuthModel(_wallet);
		Loader = new WalletLoadWorkflow(_wallet);

		this.WhenAnyValue(x => x.Auth.IsLoggedIn)
			.Where(x => x)
			.Take(1)
			.Do(_ => Loader.Start())
			.Subscribe();
	}

	public IWalletAuthModel Auth { get; }

	public IWalletLoadWorkflow Loader { get; }

	public IObservable<IChangeSet<IAddress, string>> Addresses { get; }

	private IObservable<EventPattern<ProcessedResult?>> RelevantTransactionProcessed { get; }

	public string Name => _wallet.WalletName;

	public IObservable<WalletState> State { get; }

	public IObservable<IChangeSet<TransactionSummary, uint256>> Transactions { get; }

	public WalletType WalletType { get; }

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
