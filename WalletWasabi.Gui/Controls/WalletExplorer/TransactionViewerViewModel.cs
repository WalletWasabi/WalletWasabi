using Avalonia;
using NBitcoin;
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
		private string _psbtJsonText;
		private string _psbtHexText;
		private string _psbtBase64Text;

		private CompositeDisposable Disposables { get; set; }

		public string PsbtJsonText
		{
			get => _psbtJsonText;
			set => this.RaiseAndSetIfChanged(ref _psbtJsonText, value);
		}

		public string PsbtHexText
		{
			get => _psbtHexText;
			set => this.RaiseAndSetIfChanged(ref _psbtHexText, value);
		}

		public string PsbtBase64Text
		{
			get => _psbtBase64Text;
			set => this.RaiseAndSetIfChanged(ref _psbtBase64Text, value);
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

		public void UpdatePsbt(PSBT psbt)
		{
			try
			{
				PsbtJsonText = psbt.ToString();
				PsbtHexText = psbt.ToHex();
				PsbtBase64Text = psbt.ToBase64();
			}
			catch (Exception ex)
			{
				OnException(ex);
			}
		}
	}
}
