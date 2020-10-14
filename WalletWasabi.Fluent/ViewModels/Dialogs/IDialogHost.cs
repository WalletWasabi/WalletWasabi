namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	/// <summary>
	/// Interface for ViewModels that can host a modal dialog.
	/// </summary>
	public interface IDialogHost
	{
		/// <summary>
		/// The currently active dialog. The modal dialog UI should close when this is null.
		/// </summary>
		DialogViewModelBase CurrentDialog { get; set; }
	}
}
