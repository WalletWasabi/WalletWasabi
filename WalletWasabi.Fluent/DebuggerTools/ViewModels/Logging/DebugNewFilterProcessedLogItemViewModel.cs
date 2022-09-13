using NBitcoin;
using WalletWasabi.Backend.Models;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels.Logging;

public partial class DebugNewFilterProcessedLogItemViewModel : DebugLogItemViewModel, IDisposable
{
	private readonly FilterModel _filterModel;

	public DebugNewFilterProcessedLogItemViewModel(FilterModel filterModel)
	{
		_filterModel = filterModel;

		Height = _filterModel.Header.Height;

		BlockHash = _filterModel.Header.BlockHash;

		PrevHash = _filterModel.Header.PrevHash;

		BlockTime = DateTimeOffset.FromUnixTimeSeconds(_filterModel.Header.EpochBlockTime);

		// TODO: Add FilterModel properties:
		// Filter
		// FilterKey
	}

	public uint Height { get; }

	public uint256 BlockHash { get; private set; }

	public uint256 PrevHash { get; private set; }

	public DateTimeOffset BlockTime { get; private set; }

	public void Dispose()
	{
	}
}
