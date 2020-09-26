namespace WalletWasabi.Fluent.ViewModels.Dialog
{
	public interface IDialogHost
	{
		void CloseDialog();
		void ShowDialog<TDialog>(TDialog dialogViewModel) where TDialog : DialogViewModelBase;
		DialogViewModelBase CurrentDialog { get; internal set; }
	}
}