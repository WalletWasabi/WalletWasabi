using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Base;

/// <summary>
/// CommonBase class.
/// </summary>
public abstract partial class DialogViewModelBase(UiContext uiContext) : RoutableViewModel(uiContext)
{
	[AutoNotify] private bool _isDialogOpen;
}
