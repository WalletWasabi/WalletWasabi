using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	/// <summary>
	/// CommonBase class.
	/// </summary>
	public abstract class DialogViewModelBase : ViewModelBase
	{
		private bool _isDialogOpen;

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
