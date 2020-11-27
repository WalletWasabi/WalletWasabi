using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	/// <summary>
	/// CommonBase class.
	/// </summary>
	public abstract class DialogViewModelBase : RoutableViewModel
	{
		private bool _isDialogOpen;

		public DialogViewModelBase(NavigationStateViewModel navigationState, NavigationTarget navigationTarget) : base(navigationState, navigationTarget)
		{
		}

		/// <summary>
		/// Gets or sets if the dialog is opened/closed.
		/// </summary>
		public bool IsDialogOpen
		{
			get => _isDialogOpen;
			set => this.RaiseAndSetIfChanged(ref _isDialogOpen, value);
		}
	}
}