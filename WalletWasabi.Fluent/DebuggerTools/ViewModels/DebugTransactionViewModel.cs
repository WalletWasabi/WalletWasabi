using System.Collections.Generic;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels;

public partial class DebugTransactionViewModel : ViewModelBase
{
	private readonly SmartTransaction _transaction;

	public DebugTransactionViewModel(SmartTransaction transaction)
	{
		_transaction = transaction;

		FirstSeen = transaction.FirstSeen;

		Coins = new List<DebugCoinViewModel>();
	}

	public DateTimeOffset FirstSeen { get; private set; }

	public List<DebugCoinViewModel> Coins { get; private set; }
}
