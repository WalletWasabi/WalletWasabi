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

internal class WalletModel : IWalletModel
{
	private readonly Wallet _wallet;
	private readonly TransactionHistoryBuilder _historyBuilder;

	public WalletModel(Wallet wallet)
	{
		_wallet = wallet;
		_historyBuilder = new TransactionHistoryBuilder(_wallet);

		RelevantTransactionProcessed =
			Observable.FromEventPattern<ProcessedResult?>(_wallet, nameof(_wallet.WalletRelevantTransactionProcessed))
					  .ObserveOn(RxApp.MainThreadScheduler);

		Transactions =
			Observable.Defer(() => BuildSummary().ToObservable())
					  .Concat(RelevantTransactionProcessed.SelectMany(_ => BuildSummary()))
					  .ToObservableChangeSet(x => x.TransactionId);

		Addresses = Observable
			.Defer(() => GetAddresses().ToObservable())
			.Concat(RelevantTransactionProcessed.ToSignal().SelectMany(_ => GetAddresses()))
			.ToObservableChangeSet(x => x.Text);
	}

	public IObservable<IChangeSet<IAddress, string>> Addresses { get; }

	private IObservable<EventPattern<ProcessedResult?>> RelevantTransactionProcessed { get; }

	public string Name => _wallet.WalletName;

	public IObservable<Money> Balance => throw new NotImplementedException();

	public IObservable<IChangeSet<TransactionSummary, uint256>> Transactions { get; }

	public IAddress CreateReceiveAddress(IEnumerable<string> destinationLabels)
	{
		var pubKey = _wallet.CreateReceiveAddress(destinationLabels);
		return new Address(_wallet, pubKey);
	}

	public bool IsHardwareWallet() => _wallet.KeyManager.IsHardwareWallet;

	public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent) =>
		_wallet.GetMostUsedLabels(intent);

	private IEnumerable<TransactionSummary> BuildSummary()
	{
		return _historyBuilder.BuildHistorySummary();
	}

	private IEnumerable<IAddress> GetAddresses()
	{
		return _wallet.KeyManager
					  .GetKeys()
					  .Reverse()
					  .Select(x => new Address(_wallet, x));
	}
}
