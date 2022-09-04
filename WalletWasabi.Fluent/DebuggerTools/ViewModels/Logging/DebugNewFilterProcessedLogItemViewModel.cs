using WalletWasabi.Backend.Models;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels.Logging;

public partial class DebugNewFilterProcessedLogItemViewModel : DebugLogItemViewModel
{
	private readonly FilterModel _filterModel;

	public DebugNewFilterProcessedLogItemViewModel(FilterModel filterModel)
	{
		_filterModel = filterModel;
	}
}
