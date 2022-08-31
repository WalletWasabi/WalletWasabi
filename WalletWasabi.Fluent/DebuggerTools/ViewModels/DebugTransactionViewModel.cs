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
	}

	public DateTimeOffset FirstSeen { get; private set; }
}
