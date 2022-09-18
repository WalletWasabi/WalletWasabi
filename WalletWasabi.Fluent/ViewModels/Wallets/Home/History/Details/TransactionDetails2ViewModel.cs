using System.Collections.Generic;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

public interface ITransactionViewModel : ITransaction
{
	FeeRate? FeeRate { get; }
	Money? Fee { get; }
}

[NavigationMetaData(Title = "Transaction Details")]
public partial class TransactionDetails2ViewModel : RoutableViewModel, ITransactionViewModel
{
	private readonly ITransaction _transaction;

	public TransactionDetails2ViewModel(ITransaction transaction)
	{
		_transaction = transaction;

		NextCommand = ReactiveCommand.Create(OnNext);
		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	private void OnNext()
	{
		Navigate().Clear();
	}

	public IObservable<int> Confirmations => _transaction.Confirmations.ReplayLastActive();

	public FeeRate? FeeRate => _transaction.FeeRate();

	public Money? Fee => _transaction.Fee();

	public IEnumerable<InputViewModel> Inputs => _transaction.Inputs;

	public IEnumerable<OutputViewModel> Outputs => _transaction.Outputs;

	public DateTimeOffset Timestamp => _transaction.Timestamp;

	public int IncludedInBlock => _transaction.IncludedInBlock;

	public Money Amount => _transaction.Amount;

	public string Id => _transaction.Id;

	public double Size => _transaction.Size;

	public int Version => _transaction.Version;

	public long BlockTime => _transaction.BlockTime;

	public double Weight => _transaction.Weight;

	public double VirtualSize => _transaction.VirtualSize;

	public IEnumerable<string> Labels => _transaction.Labels;

	public Money? InputAmount => _transaction.InputAmount;

	public Money OutputAmount => _transaction.OutputAmount;
}
