using Avalonia;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionBroadcasterViewModel : WalletActionViewModel
	{
		private string _transactionString;
		private string _errorMessage;
		private string _successMessage;

		private CompositeDisposable Disposables { get; set; }
		public ReactiveCommand<Unit, Unit> PasteCommand { get; set; }

		public string TransactionString
		{
			get => _transactionString;
			set => this.RaiseAndSetIfChanged(ref _transactionString, value);
		}

		public string ErrorMessage
		{
			get => _errorMessage;
			set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
		}

		public string SuccessMessage
		{
			get => _successMessage;
			set => this.RaiseAndSetIfChanged(ref _successMessage, value);
		}

		public TransactionBroadcasterViewModel(WalletViewModel walletViewModel) : base("Transaction Broadcaster", walletViewModel)
		{
			PasteCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				if (!string.IsNullOrEmpty(TransactionString)) return;
				var textToPaste = await Application.Current.Clipboard.GetTextAsync();
				TransactionString = textToPaste;
			});

			Observable.Merge(PasteCommand.ThrownExceptions).Subscribe(OnException);
		}

		private static bool IsValidTransaction(string txstring)
		{
			return false;
		}

		private void OnTransactionTextChanged(string text)
		{
			try
			{
				if (!IsValidTransaction(null))
				{
				}
			}
			catch (Exception ex)
			{
				OnException(ex);
			}
		}

		private void OnException(Exception ex)
		{
			ErrorMessage = ex.ToTypeMessageString();
		}

		public override void OnOpen()
		{
			if (Disposables != null)
			{
				throw new Exception("TransactionBroadcaster was opened before it was closed.");
			}

			Disposables = new CompositeDisposable();

			var tc = this.WhenAnyValue(x => x.TransactionString)
				.Subscribe(OnTransactionTextChanged);

			base.OnOpen();
		}

		public override bool OnClose()
		{
			Disposables?.Dispose();
			Disposables = null;

			return base.OnClose();
		}
	}
}
