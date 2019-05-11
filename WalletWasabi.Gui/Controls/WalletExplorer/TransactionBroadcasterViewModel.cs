using Avalonia;
using AvalonStudio.Documents;
using NBitcoin;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionBroadcasterViewModel : WalletActionViewModel, IDocumentTabViewModel
	{
		private string _transactionString;
		private bool _isBusy;
		private string _buttonText;
		private int _caretIndex;

		private CompositeDisposable Disposables { get; set; }
		public ReactiveCommand<Unit, Unit> PasteCommand { get; set; }
		public ReactiveCommand<Unit, Unit> BroadcastTransactionCommand { get; set; }

		public string TransactionString
		{
			get => _transactionString;
			set => this.RaiseAndSetIfChanged(ref _transactionString, value);
		}

		public bool IsBusy
		{
			get => _isBusy;
			set => this.RaiseAndSetIfChanged(ref _isBusy, value);
		}

		public string ButtonText
		{
			get => _buttonText;
			set => this.RaiseAndSetIfChanged(ref _buttonText, value);
		}

		public int CaretIndex
		{
			get => _caretIndex;
			set => this.RaiseAndSetIfChanged(ref _caretIndex, value);
		}

		public TransactionBroadcasterViewModel(WalletViewModel walletViewModel) : base("Transaction Broadcaster", walletViewModel)
		{
			ButtonText = "Broadcast Transaction";

			PasteCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				if (!string.IsNullOrEmpty(TransactionString))
				{
					return;
				}

				var textToPaste = await Application.Current.Clipboard.GetTextAsync();
				TransactionString = textToPaste;
			});

			BroadcastTransactionCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				await OnDoTransactionBroadcastAsync();
			});

			Observable.Merge(PasteCommand.ThrownExceptions)
				.Merge(BroadcastTransactionCommand.ThrownExceptions)
				.Subscribe(OnException);
		}

		private void OnException(Exception ex)
		{
			SetWarningMessage(ex.ToTypeMessageString());
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

			TransactionString = "";

			return base.OnClose();
		}

		private async Task OnDoTransactionBroadcastAsync()
		{
			const string broadcastingTransactionStatusText = "Broadcasting transaction...";
			try
			{
				IsBusy = true;
				ButtonText = "Broadcasting Transaction...";

				SmartTransaction transaction;
				try
				{
					var signedPsbt = PSBT.Parse(TransactionString, Global.Network ?? Network.Main);

					if (!signedPsbt.IsAllFinalized())
					{
						signedPsbt.Finalize();
					}

					transaction = signedPsbt.ExtractSmartTransaction();
				}
				catch
				{
					transaction = new SmartTransaction(Transaction.Parse(TransactionString, Global.Network ?? Network.Main), WalletWasabi.Models.Height.Unknown);
				}

				MainWindowViewModel.Instance.StatusBar.TryAddStatus(broadcastingTransactionStatusText);
				await Task.Run(async () => await Global.WalletService.SendTransactionAsync(transaction));

				SetSuccessMessage("Transaction is successfully sent!");
				TransactionString = "";
			}
			catch (Exception ex)
			{
				OnException(ex);
			}
			finally
			{
				MainWindowViewModel.Instance.StatusBar.TryRemoveStatus(broadcastingTransactionStatusText);
				IsBusy = false;
				ButtonText = "Broadcast Transaction";
			}
		}
	}
}
