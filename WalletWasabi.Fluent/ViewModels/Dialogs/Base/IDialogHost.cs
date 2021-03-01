namespace WalletWasabi.Fluent.ViewModels.Dialogs.Base
{
	/// <summary>
	/// Interface for ViewModels that can host a modal dialog.
	/// </summary>
	public interface IDialogHost
	{
		/// <summary>
		/// The currently active dialog. The modal dialog UI should close when this is null.
		/// </summary>
		Base.DialogViewModelBase? CurrentDialog { get; set; }
	}
}
