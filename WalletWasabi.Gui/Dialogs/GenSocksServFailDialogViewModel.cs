using AvalonStudio.Extensibility.Dialogs;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;

namespace WalletWasabi.Gui.Dialogs
{
	public class GenSocksServFailDialogViewModel : ModalDialogViewModelBase
	{
		public GenSocksServFailDialogViewModel() : base("", true, false)
		{
			OKCommand = ReactiveCommand.Create(() =>
			{
				// OK pressed.
				Close(true);
			});
		}
	}
}
