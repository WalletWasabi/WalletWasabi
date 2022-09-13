using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels.Logging;

public partial class DebugNewBlockProcessedLogItemViewModel : DebugLogItemViewModel, IDisposable
{
	private readonly Block _block;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private List<uint256>? _transactions;

	public DebugNewBlockProcessedLogItemViewModel(Block block)
	{
		_block = block;

		BlockTime = block.Header.BlockTime;

		CoinbaseHeight = block.GetCoinbaseHeight();

		BlockId = block.GetHash();

		Transactions = _block.Transactions.Select(x => x.GetHash()).ToList();

		// TODO: Add Block properties.
	}

	public Block Block => _block;

	public DateTimeOffset BlockTime { get; private set; }

	public int? CoinbaseHeight { get; private set; }

	public uint256 BlockId { get; private set; }

	public void Dispose()
	{
	}
}
