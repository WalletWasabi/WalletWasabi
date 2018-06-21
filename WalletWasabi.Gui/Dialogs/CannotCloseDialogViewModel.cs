using AvalonStudio.Extensibility.Dialogs;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;

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
