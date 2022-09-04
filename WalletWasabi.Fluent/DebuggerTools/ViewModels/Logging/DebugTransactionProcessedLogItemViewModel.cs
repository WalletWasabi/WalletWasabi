using WalletWasabi.Blockchain.TransactionProcessing;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels.Logging;

public partial class DebugTransactionProcessedLogItemViewModel : DebugLogItemViewModel
{
	private readonly ProcessedResult _processedResult;

	public DebugTransactionProcessedLogItemViewModel(ProcessedResult processedResult)
	{
		_processedResult = processedResult;
	}
}
