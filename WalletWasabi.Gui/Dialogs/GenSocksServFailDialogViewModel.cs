using AvalonStudio.Extensibility.Dialogs;
using ReactiveUI;

namespace WalletWasabi.Gui.Dialogs
{
	public class GenSocksServFailDialogViewModel : ModalDialogViewModelBase
	{
		public GenSocksServFailDialogViewModel() : base("", true, false)
		{
			OKCommand = ReactiveCommand.Create(() => Close(true)); // OK pressed.
		}
	}
}
