using System.Collections.Generic;
using NBitcoin;
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

		TransactionId = transaction.GetHash();

		Coins = new List<DebugCoinViewModel>();
	}

	public DateTimeOffset FirstSeen { get; private set; }

	public uint256 TransactionId { get; private set; }

	public List<DebugCoinViewModel> Coins { get; private set; }
}
