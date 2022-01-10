using WalletWasabi.Fluent.ViewModels.NavBar;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Base;

/// <summary>
/// CommonBase class.
/// </summary>
public abstract partial class DialogViewModelBase : NavBarItemViewModel
{
	[AutoNotify] private bool _isDialogOpen;
}
