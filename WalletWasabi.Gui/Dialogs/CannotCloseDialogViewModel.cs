using AvalonStudio.Extensibility.Dialogs;
using ReactiveUI;

namespace WalletWasabi.Gui.Dialogs
{
	internal class CannotCloseDialogViewModel : ModalDialogViewModelBase
	{
		public CannotCloseDialogViewModel() : base("", true, false)
		{
			OKCommand = ReactiveCommand.Create(() =>
			{
				// OK pressed.
				Close(true);
			});
		}
	}
}
