using System.Collections.Generic;
using System.Reactive;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels;

public partial class DebugTransactionViewModel : ViewModelBase
{
	private readonly SmartTransaction _transaction;
	private readonly IObservable<Unit> _updateTrigger;

	public DebugTransactionViewModel(SmartTransaction transaction, IObservable<Unit> updateTrigger)
	{
		_transaction = transaction;
		_updateTrigger = updateTrigger;

		Update();
	}

	private void Update()
	{
		FirstSeen = _transaction.FirstSeen;

		TransactionId = _transaction.GetHash();

		Coins = new List<DebugCoinViewModel>();
	}

	public DateTimeOffset FirstSeen { get; private set; }

	public uint256 TransactionId { get; private set; }

	public List<DebugCoinViewModel> Coins { get; private set; }
}
