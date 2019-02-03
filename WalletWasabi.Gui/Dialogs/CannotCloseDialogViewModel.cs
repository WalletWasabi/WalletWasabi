using Avalonia;
using Avalonia.Threading;
using AvalonStudio.Extensibility.Dialogs;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Dialogs
{
	internal class CannotCloseDialogViewModel : ModalDialogViewModelBase, IDisposable
	{
		private bool _isBusy;
		private string _warningMessage;
		private string _operationMessage;
		private CancellationTokenSource _cancelTokenSource;
		private CompositeDisposable Disposables { get; } = new CompositeDisposable();

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

		public string OperationMessage
		{
			get => _operationMessage;
			set => this.RaiseAndSetIfChanged(ref _operationMessage, value);
		}

		public new ReactiveCommand OKCommand { get; set; }
		public new ReactiveCommand AbortCommand { get; set; }

		//http://blog.stephencleary.com/2013/01/async-oop-2-constructors.html
		public Task Initialization { get; private set; }

		public CannotCloseDialogViewModel() : base("", false, false)
		{
			OperationMessage = "Dequeuing coins...Please wait";
			var canAbort = this.WhenAnyValue(x => x.IsBusy);
			var canOk = this.WhenAnyValue(x => x.IsBusy, (isbusy) => !isbusy);

			OKCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				_cancelTokenSource.Cancel();
				while (!Initialization.IsCompleted)
				{
					await Task.Delay(300);
				}
				// OK pressed.
				Close(false);
			},
			canOk);

			AbortCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				OperationMessage = "Cancelling...Please wait";
				_cancelTokenSource.Cancel();
				while (!Initialization.IsCompleted)
				{
					await Task.Delay(300);
				}
				// OK pressed.
				Close(false);
			},
			canAbort);

			_cancelTokenSource = new CancellationTokenSource();
			Disposables.Add(_cancelTokenSource);

			OKCommand.ThrownExceptions.Subscribe(ex => Logging.Logger.LogWarning<CannotCloseDialogViewModel>(ex));
			AbortCommand.ThrownExceptions.Subscribe(ex => Logging.Logger.LogWarning<CannotCloseDialogViewModel>(ex));

			Initialization = StartDequeueAsync(_cancelTokenSource.Token);
		}

		private async Task StartDequeueAsync(CancellationToken token)
		{
			IsBusy = true;
			try
			{
				DateTime start = DateTime.Now;

				while (!IsVisible) //waiting for the window to show. TODO: add OnShow ModalDialogViewModelBase.
				{
					//If this is not waited than ModalDialogViewModelBase.dialogCloseCompletionSource will throw NRF when Close(true) called
					await Task.Delay(300);
					if (DateTime.Now - start > TimeSpan.FromSeconds(10))
						throw new InvalidOperationException("Window not opened");
				}

				start = DateTime.Now;
				bool last = false;
				while (!last)
				{
					last = DateTime.Now - start > TimeSpan.FromMinutes(2);
					if (_cancelTokenSource.IsCancellationRequested) break;
					try
					{
						if (Global.WalletService is null || Global.ChaumianClient is null)
							return;
						SmartCoin[] enqueuedCoins = Global.WalletService.Coins.Where(x => x.CoinJoinInProgress).ToArray();
						Exception latestException = null;
						foreach (var coin in enqueuedCoins)
						{
							try
							{
								if (_cancelTokenSource.IsCancellationRequested) break;
								await Global.ChaumianClient.DequeueCoinsFromMixAsync(new SmartCoin[] { coin }); //dequeue coins one-by-one to check abort flag more frequently
							}
							catch (Exception ex)
							{
								latestException = ex;

								if (last) //if this is the last iteration and we are still failing then we throw the exception
									throw ex;
							}
						}

						if (latestException is null) //no exceptions were thrown during the for-each so we are done with dequeuing
							last = true;
					}
					catch (Exception ex)
					{
						if (last) throw ex;
						await Task.Delay(5000, token); //wait, maybe the situation will change
					}
				}
				if (!_cancelTokenSource.IsCancellationRequested)
				{
					_cancelTokenSource.Cancel();
					Close(true);
				}
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

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables.Dispose();
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
