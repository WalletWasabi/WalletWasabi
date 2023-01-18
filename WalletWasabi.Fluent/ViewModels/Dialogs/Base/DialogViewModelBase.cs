using CommunityToolkit.Mvvm.ComponentModel;
using WalletWasabi.Fluent.ViewModels.NavBar;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Base;

/// <summary>
/// CommonBase class.
/// </summary>
public abstract partial class DialogViewModelBase : NavBarItemViewModel
{
	[ObservableProperty] private bool _isDialogOpen;
}
