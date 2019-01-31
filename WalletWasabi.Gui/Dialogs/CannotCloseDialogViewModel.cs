using Avalonia;
using Avalonia.Threading;
using AvalonStudio.Extensibility.Dialogs;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi;

namespace WalletWasabi.Gui.Dialogs
{
	internal class CannotCloseDialogViewModel : ModalDialogViewModelBase
	{
		private bool _isBusy;
		private string _warningMessage;
		private CancellationTokenSource _cancelTokenSource;

		public bool IsBusy
		{
			get => _isBusy;
			set => this.RaiseAndSetIfChanged(ref _isBusy, value);
		}

		public string WarningMessage
		{
			get => _warningMessage;
			set => this.RaiseAndSetIfChanged(ref _warningMessage, value);
		}

		//http://blog.stephencleary.com/2013/01/async-oop-2-constructors.html
		public Task Initialization { get; private set; }

		public CannotCloseDialogViewModel() : base("", true, false)
		{
			OKCommand = ReactiveCommand.Create(() =>
			{
				_cancelTokenSource.Cancel();
				// OK pressed.
				Close(true);
			});

			IsBusy = true;
			_cancelTokenSource = new CancellationTokenSource();
			Initialization = StartDequeueAsync(_cancelTokenSource.Token);
		}

		private async Task StartDequeueAsync(CancellationToken token)
		{
			try
			{
				DateTime start = DateTime.Now;
				bool last = false;
				while (!last)
				{
					last = DateTime.Now - start > TimeSpan.FromMinutes(2);
					try
					{
						await Global.DesperateDequeueAllCoinsAsync();
						last = true;
					}
					catch (Exception ex)
					{
						if (last) throw ex;
						await Task.Delay(5000, token); //wait, maybe the situation will change
					}
				}
				_cancelTokenSource.Cancel();
				Dispatcher.UIThread.Post(() =>
				{
					try
					{
						Global.QuitApplication();
					}
					catch (Exception) { }
				});
			}
			catch (Exception ex)
			{
				SetWarningMessage(ex.Message, token);
			}
			finally
			{
				IsBusy = false;
			}
		}

		private void SetWarningMessage(string message, CancellationToken token)
		{
			WarningMessage = message;

			Dispatcher.UIThread.Post(async () =>
			{
				try
				{
					await Task.Delay(7000, token);
				}
				catch (Exception) { }

				if (WarningMessage == message)
				{
					WarningMessage = "";
				}
			});
		}
	}
}
