using Avalonia.Threading;
using AvalonStudio.Extensibility.Dialogs;
using ReactiveUI;
using Splat;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Dialogs
{
	public class CannotCloseDialogViewModel : ModalDialogViewModelBase
	{
		private bool _isBusy;
		private string _warningMessage;
		private string _operationMessage;

		public CannotCloseDialogViewModel() : base("", false, false)
		{
			Global = Locator.Current.GetService<Global>();
			OperationMessage = "Dequeuing coins...Please wait";
			var canCancel = this.WhenAnyValue(x => x.IsBusy);
			var canOk = this.WhenAnyValue(x => x.IsBusy, (isbusy) => !isbusy);

			OKCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				CancelTokenSource.Cancel();
				while (!Initialization.IsCompleted)
				{
					await Task.Delay(300);
				}
				// OK pressed.
				Close(false);
			},
			canOk);

			CancelCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				OperationMessage = "Cancelling...Please wait";
				CancelTokenSource.Cancel();
				while (!Initialization.IsCompleted)
				{
					await Task.Delay(300);
				}
				// OK pressed.
				Close(false);
			},
			canCancel);

			Observable
				.Merge(OKCommand.ThrownExceptions)
				.Merge(CancelCommand.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		private CompositeDisposable Disposables { get; set; }

		private CancellationTokenSource CancelTokenSource { get; set; }

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

		private Global Global { get; }

		public string OperationMessage
		{
			get => _operationMessage;
			set => this.RaiseAndSetIfChanged(ref _operationMessage, value);
		}

		public new ReactiveCommand<Unit, Unit> OKCommand { get; set; }
		public new ReactiveCommand<Unit, Unit> CancelCommand { get; set; }

		// http://blog.stephencleary.com/2013/01/async-oop-2-constructors.html
		public Task Initialization { get; private set; }

		public override void OnOpen()
		{
			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			CancelTokenSource = new CancellationTokenSource().DisposeWith(Disposables);

			Initialization = StartDequeueAsync();

			base.OnOpen();
		}

		public override void OnClose()
		{
			Disposables?.Dispose();
			Disposables = null;
			base.OnClose();
		}

		private async Task StartDequeueAsync()
		{
			IsBusy = true;
			try
			{
				DateTime start = DateTime.Now;

				while (!IsVisible) // waiting for the window to show. TODO: add OnShow ModalDialogViewModelBase.
				{
					// If this is not waited than ModalDialogViewModelBase.dialogCloseCompletionSource will throw NRF when Close(true) called
					await Task.Delay(300);
					if (DateTime.Now - start > TimeSpan.FromSeconds(10))
					{
						throw new InvalidOperationException("Window did not open.");
					}
				}

				using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(6));
				using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, CancelTokenSource.Token);
				await Global.WalletManager.DequeueAllCoinsGracefullyAsync(DequeueReason.ApplicationExit, linkedCts.Token);

				if (!CancelTokenSource.IsCancellationRequested)
				{
					CancelTokenSource.Cancel();
					Close(true);
				}
			}
			catch (Exception ex)
			{
				SetWarningMessage(ex.Message, CancelTokenSource.Token);
			}
			finally
			{
				IsBusy = false;
			}
		}

		private void SetWarningMessage(string message, CancellationToken token)
		{
			WarningMessage = message;

			Dispatcher.UIThread.PostLogException(async () =>
			{
				try
				{
					await Task.Delay(7000, token);
				}
				catch (TaskCanceledException ex)
				{
					Logger.LogTrace(ex);
				}

				if (WarningMessage == message)
				{
					WarningMessage = "";
				}
			});
		}
	}
}
