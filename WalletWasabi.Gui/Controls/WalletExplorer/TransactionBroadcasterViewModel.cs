using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionBroadcasterViewModel : WalletActionViewModel
	{
		private string _transactionString;
		private string _errorMessage;
		private string _successMessage;

		private CompositeDisposable Disposables { get; set; }

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
		}

		private static bool IsValidTransaction(string txstring)
		{
			return false;
		}

		private void OnTransactionTextChanged()
		{
			if (!IsValidTransaction(null))
			{
			}
		}

		public override void OnOpen()
		{
			if (Disposables != null)
			{
				throw new Exception("TransactionBroadcaster was opened before it was closed.");
			}

			Disposables = new CompositeDisposable();

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
