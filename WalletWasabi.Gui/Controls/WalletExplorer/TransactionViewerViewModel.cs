using Avalonia;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionViewerViewModel : WalletActionViewModel
	{
		private string _errorMessage;
		private string _successMessage;

		private CompositeDisposable Disposables { get; set; }

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

		public TransactionViewerViewModel(WalletViewModel walletViewModel) : base("Transaction Viewer", walletViewModel)
		{
		}

		private void OnException(Exception ex)
		{
			ErrorMessage = ex.ToTypeMessageString();
		}

		public override void OnOpen()
		{
			if (Disposables != null)
			{
				throw new Exception("Transaction Viewer was opened before it was closed.");
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
