using System.Reactive;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.TransactionProcessing;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels.Logging;

public partial class DebugTransactionProcessedLogItemViewModel : DebugLogItemViewModel
{
	private readonly ProcessedResult _processedResult;

	public DebugTransactionProcessedLogItemViewModel(ProcessedResult processedResult)
	{
		_processedResult = processedResult;

		Transaction = new DebugTransactionViewModel(_processedResult.Transaction, Observable.Empty<Unit>());

		IsNews = _processedResult.IsNews;

		IsOwnCoinJoin = _processedResult.IsOwnCoinJoin;

		// TODO: Add ProcessedResult properties:
		// ReceivedDusts
		// ReceivedCoins
		// NewlyReceivedCoins
		// NewlyConfirmedReceivedCoins
		// SpentCoins
		// NewlySpentCoins
		// NewlyConfirmedSpentCoins
		// SuccessfullyDoubleSpentCoins
		// ReplacedCoins
		// RestoredCoins
	}

	public DebugTransactionViewModel Transaction { get; private set; }

	public bool IsNews { get; private set; }

	public bool IsOwnCoinJoin { get; private set; }
}
