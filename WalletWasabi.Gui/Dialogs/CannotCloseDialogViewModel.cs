using AvalonStudio.Extensibility.Dialogs;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Dialogs
{
	internal class CannotCloseDialogViewModel : ModalDialogViewModelBase, IAsyncInitialization
	{
		private bool _isBusy;

		public bool IsBusy
		{
			get => _isBusy;
			set => this.RaiseAndSetIfChanged(ref _isBusy, value);
		}

		//http://blog.stephencleary.com/2013/01/async-oop-2-constructors.html
		public Task Initialization { get; private set; }

		public CannotCloseDialogViewModel() : base("", true, true)
		{
			OKCommand = ReactiveCommand.Create(() =>
			{
				// OK pressed.
				Close(true);
			});

			IsBusy = true;

			Initialization = StartDequeueAsync();
		}

		private async Task StartDequeueAsync()
		{
			try
			{
				await Global.TryDesperateDequeueAllCoinsAsync();
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, nameof(Global));
			}
			finally
			{
				IsBusy = false;
			}
		}
	}
}
