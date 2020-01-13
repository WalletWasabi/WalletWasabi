using AvalonStudio.Extensibility.Dialogs;
using ReactiveUI;
using WalletWasabi.Logging;
using System.Reactive.Linq;
using System;

namespace WalletWasabi.Gui.Dialogs
{
	public class GenSocksServFailDialogViewModel : ModalDialogViewModelBase
	{
		public GenSocksServFailDialogViewModel() : base("", true, false)
		{
			OKCommand = ReactiveCommand.Create(() => Close(true)); // OK pressed.
			OKCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}
	}
}
