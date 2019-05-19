using Avalonia;
using NBitcoin;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionViewerViewModel : WalletActionViewModel
	{
		private string _psbtJsonText;
		private string _psbtHexText;
		private string _psbtBase64Text;

		private CompositeDisposable Disposables { get; set; }

		public string PsbtJsonText
		{
			get => _psbtJsonText;
			set => this.RaiseAndSetIfChanged(ref _psbtJsonText, value);
		}

		public string TransactionHexText
		{
			get => _psbtHexText;
			set => this.RaiseAndSetIfChanged(ref _psbtHexText, value);
		}

		public string PsbtBase64Text
		{
			get => _psbtBase64Text;
			set => this.RaiseAndSetIfChanged(ref _psbtBase64Text, value);
		}

		public TransactionViewerViewModel(WalletViewModel walletViewModel) : base("Transaction", walletViewModel)
		{
		}

		private void OnException(Exception ex)
		{
			SetWarningMessage(ex.ToTypeMessageString());
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

		public void Update(BuildTransactionResult result)
		{
			try
			{
				PsbtJsonText = result.Psbt.ToString();
				TransactionHexText = result.Transaction.Transaction.ToHex();
				PsbtBase64Text = result.Psbt.ToBase64();
			}
			catch (Exception ex)
			{
				OnException(ex);
			}
		}
	}
}
