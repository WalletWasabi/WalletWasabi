namespace WalletWasabi.Fluent.ViewModels.Dialog
{
	/// <summary>
	/// Interface for ViewModels that can host a modal dialog.
	/// </summary>
	public interface IDialogHost
	{
		/// <summary>
		/// Close the currently displayed dialog.
		/// </summary>
		void CloseDialog();

		/// <summary>
		/// Show a dialog from a ViewModel with <see cref="DialogViewModelBase{TResult}"/> as its base.
		/// </summary>
		/// <param name="dialogViewModel">The instance of the Dialog ViewModel to be displayed</param>
		void ShowDialog<TDialog>(TDialog dialogViewModel) where TDialog : DialogViewModelBase;
		
		/// <summary>
		/// The currently active dialog. The modal dialog UI should close when this is null.
		/// </summary>
		DialogViewModelBase CurrentDialog { get; }
	}
}