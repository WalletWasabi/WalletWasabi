using NBitcoin;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels.Logging;

public partial class DebugNewBlockProcessedLogItemViewModel : DebugLogItemViewModel
{
	private readonly Block _block;

	public DebugNewBlockProcessedLogItemViewModel(Block block)
	{
		_block = block;

		// TODO: Add Block properties.
	}
}
