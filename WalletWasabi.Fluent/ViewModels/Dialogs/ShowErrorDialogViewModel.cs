using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public partial class ShowErrorDialogViewModel : DialogViewModelBase
	{
		[AutoNotify] private string _message;

		public ShowErrorDialogViewModel(string message)
		{
			_message = message;
		}
	}
}