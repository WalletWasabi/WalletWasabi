using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Base
{
	/// <summary>
	/// CommonBase class.
	/// </summary>
	public abstract partial class DialogViewModelBase : NavBarItemViewModel
	{
		[AutoNotify] private bool _isDialogOpen;
	}
}
