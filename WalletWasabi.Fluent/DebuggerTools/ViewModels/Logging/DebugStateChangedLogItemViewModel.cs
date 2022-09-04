using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels.Logging;

public partial class DebugStateChangedLogItemViewModel : DebugLogItemViewModel
{
	private readonly WalletState _state;

	public DebugStateChangedLogItemViewModel(WalletState state)
	{
		_state = state;
	}

	public WalletState State => _state;
}
