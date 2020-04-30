using Avalonia.Controls.Notifications;
using NBitcoin;
using NBitcoin.Payment;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models.StatusBarStatuses;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Models;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.PayJoin;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class SendTabViewModel : SendControlViewModel
	{
		private string _payjoinEndPoint;

		public SendTabViewModel(Wallet wallet) : base(wallet, "Send")
		{
		}

		public override string DoButtonText => "Send Transaction";
		public override string DoingButtonText => "Sending Transaction...";

		protected override async Task BuildTransaction(string password, PaymentIntent payments, FeeStrategy feeStrategy, bool allowUnconfirmed = false, IEnumerable<OutPoint> allowedInputs = null)
		{
			BuildTransactionResult result = await Task.Run(() => Wallet.BuildTransaction(Password, payments, feeStrategy, allowUnconfirmed: true, allowedInputs: allowedInputs, GetPayjoinClient()));

			MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusType.SigningTransaction);
			SmartTransaction signedTransaction = result.Transaction;

			if (Wallet.KeyManager.IsHardwareWallet && !result.Signed) // If hardware but still has a privkey then it's password, then meh.
			{
				try
				{
					IsHardwareBusy = true;
					MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusType.AcquiringSignatureFromHardwareWallet);
					var client = new HwiClient(Global.Network);

					using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
					PSBT signedPsbt = null;
					try
					{
						signedPsbt = await client.SignTxAsync(Wallet.KeyManager.MasterFingerprint.Value, result.Psbt, cts.Token);
					}
					catch (HwiException)
					{
						await PinPadViewModel.UnlockAsync();
						signedPsbt = await client.SignTxAsync(Wallet.KeyManager.MasterFingerprint.Value, result.Psbt, cts.Token);
					}
					signedTransaction = signedPsbt.ExtractSmartTransaction(result.Transaction);
				}
				finally
				{
					MainWindowViewModel.Instance.StatusBar.TryRemoveStatus(StatusType.AcquiringSignatureFromHardwareWallet);
					IsHardwareBusy = false;
				}
			}

			MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusType.BroadcastingTransaction);
			await Task.Run(async () => await Global.TransactionBroadcaster.SendTransactionAsync(signedTransaction));

			ResetUi();
		}

		[ValidateMethod(nameof(ValidatePayjoinEndPoint))]
		public string PayjoinEndPoint
		{
			get => _payjoinEndPoint;
			set => this.RaiseAndSetIfChanged(ref _payjoinEndPoint, value);
		}

		private IPayjoinClient GetPayjoinClient()
		{
			if (!string.IsNullOrWhiteSpace(PayjoinEndPoint) && Uri.IsWellFormedUriString(PayjoinEndPoint, UriKind.Absolute))
			{
				var payjoinEndPointUri = new Uri(PayjoinEndPoint);
				return new PayjoinClient(payjoinEndPointUri, Global.TorManager.TorSocks5EndPoint);
			}

			return null;
		}

		public ErrorDescriptors ValidatePayjoinEndPoint()
		{
			if (string.IsNullOrWhiteSpace(PayjoinEndPoint) || Uri.IsWellFormedUriString(PayjoinEndPoint, UriKind.Absolute))
			{
				return ErrorDescriptors.Empty;
			}
			return new ErrorDescriptors(new ErrorDescriptor(ErrorSeverity.Error, "Invalid url."));
		}

		protected override void OnAddressPaste(BitcoinUrlBuilder url)
		{
			base.OnAddressPaste(url);

			if (url.UnknowParameters.TryGetValue("bpu", out var endPoint) || url.UnknowParameters.TryGetValue("pj", out endPoint))
			{
				if (!Wallet.KeyManager.IsWatchOnly)
				{
					PayjoinEndPoint = endPoint;
					return;
				}
				NotificationHelpers.Warning("Payjoin is not allowed here.");
			}
			PayjoinEndPoint = null;
		}

		protected override void ResetUi()
		{
			base.ResetUi();
			PayjoinEndPoint = "";
		}
	}
}
