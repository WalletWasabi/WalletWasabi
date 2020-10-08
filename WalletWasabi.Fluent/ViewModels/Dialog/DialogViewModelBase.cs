using System;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Dialog
{
	/// <summary>
	/// Foundational class for <see cref="DialogViewModelBase{TResult}"/>.
	/// Don't use this class directly since it doesn't provide the
	/// functionality required for Dialogs.
	/// </summary>	
	public abstract class DialogViewModelBase : ViewModelBase
	{
		private bool _isDialogOpen;
 
		public bool IsDialogOpen
		{
			get => _isDialogOpen;
			set => this.RaiseAndSetIfChanged(ref _isDialogOpen, value);
		}

		/// <summary>
		/// Method that is triggered when the dialog
		/// is to be shown.
		/// </summary>
		protected abstract void DialogShowing();

		/// <summary>
		/// Method that is triggered when the dialog
		/// is about to close.
		/// </summary>
		protected abstract void DialogClosing();

		/// <summary>
		/// Method to be called when the dialog intends to close
		/// without returning a value.
		/// </summary>
		public void CloseDialog()
		{
			if (IsDialogOpen is false)
			{
				throw new InvalidOperationException("Dialog was already closed.");
			} 

			DialogClosing();
			IsDialogOpen = false;
		}
	}
}
