using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions.Summary;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public class ReactiveTransaction : ITransaction
{
	private readonly TransactionHistoryBuilder _historyBuilder;
	private readonly Wallet _wallet;

	public ReactiveTransaction(Wallet wallet, uint256 transactionId)
	{
		_wallet = wallet;
		_historyBuilder = new TransactionHistoryBuilder(wallet);
		var transaction = GetTransaction(transactionId);
		Id = transaction.TransactionId.ToString();
		Timestamp = transaction.DateTime;
		IncludedInBlock = transaction.Height;
		Version = transaction.Version;
		BlockTime = transaction.BlockTime;
		VirtualSize = transaction.VirtualSize;
		Size = transaction.Size;
		Weight = transaction.Weight;

		Inputs = transaction.Inputs.Select(
			x => x switch
		{
			UnknownInput b => (InputViewModel)new UnknownInputViewModel(b.TransactionId),
			InputAmount a => new KnownInputViewModel(a.Amount, a.Address.ToString()),
			_ => throw new NotSupportedException("Invalid input type")
		});

		Outputs = transaction.Outputs.Select(x => new OutputViewModel(x.Amount, x.Destination.ToString(), x.IsSpent));
		Labels = transaction.Label.Labels;
		Amount = transaction.Amount;
		Confirmations = GetSomethingChanged()
			.Select(_ => GetTransaction(transactionId).GetConfirmations())
			.StartWith(transaction.GetConfirmations());
	}

	public IEnumerable<string> Labels { get; }
	public IObservable<int> Confirmations { get; }
	public IEnumerable<InputViewModel> Inputs { get; }
	public IEnumerable<OutputViewModel> Outputs { get; }
	public DateTimeOffset Timestamp { get; }
	public int IncludedInBlock { get; }
	public Money Amount { get; }
	public string Id { get; }
	public double Size { get; }
	public int Version { get; }
	public long BlockTime { get; }
	public double Weight { get; }
	public double VirtualSize { get; }

	private TransactionSummary GetTransaction(uint256 id)
	{
		var summary = _historyBuilder.BuildHistorySummary();
		return summary.First(tx => tx.TransactionId == id);
	}

	private IObservable<Unit> GetSomethingChanged()
	{
		return
			Observable.FromEventPattern(
					_wallet.TransactionProcessor,
					nameof(_wallet.TransactionProcessor.WalletRelevantTransactionProcessed))
				.Select(_ => Unit.Default)
				.Merge(
					Observable.FromEventPattern(_wallet, nameof(_wallet.NewFilterProcessed))
						.Select(_ => Unit.Default))
				.Merge(Services.UiConfig.WhenAnyValue(x => x.PrivacyMode).Select(_ => Unit.Default))
				.Merge(_wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate).Select(_ => Unit.Default));
	}

	
}
