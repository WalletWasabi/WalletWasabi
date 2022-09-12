using NBitcoin;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels.Logging;

public partial class DebugNewBlockProcessedLogItemViewModel : DebugLogItemViewModel
{
	private readonly Block _block;

	public DebugNewBlockProcessedLogItemViewModel(Block block)
	{
		_block = block;

		BlockTime = block.Header.BlockTime;

		CoinbaseHeight = block.GetCoinbaseHeight();

		BlockId = block.GetHash();

		// TODO: Add Block properties e.g. Transactions.
	}

	public Block Block => _block;

	public DateTimeOffset BlockTime { get; private set; }

	public int? CoinbaseHeight { get; private set; }

	public uint256 BlockId { get; private set; }
}
