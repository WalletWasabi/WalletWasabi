using NBitcoin;
using NBitcoin.Payment;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models.StatusBarStatuses;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Models;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.PayJoin;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class SendTabViewModel : SendControlViewModel
	{
		private string _payjoinEndPoint;

		public SendTabViewModel(Wallet wallet) : base(wallet, "Send")
		{
			this.ValidateProperty(x => x.PayjoinEndPoint, ValidatePayjoinEndPoint);
		}

		public override string DoButtonText => "Send Transaction";
		public override string DoingButtonText => "Sending Transaction...";

		public string PayjoinEndPoint
		{
			get => _payjoinEndPoint;
			set => this.RaiseAndSetIfChanged(ref _payjoinEndPoint, value);
		}

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
					catch (HwiException ex) when (ex.ErrorCode is not HwiErrorCode.ActionCanceled)
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

		private IPayjoinClient? GetPayjoinClient()
		{
			if (!string.IsNullOrWhiteSpace(PayjoinEndPoint) &&
				Uri.IsWellFormedUriString(PayjoinEndPoint, UriKind.Absolute))
			{
				var payjoinEndPointUri = new Uri(PayjoinEndPoint);
				if (!Global.Config.UseTor)
				{
					if (payjoinEndPointUri.DnsSafeHost.EndsWith(".onion", StringComparison.OrdinalIgnoreCase))
					{
						Logger.LogWarning("PayJoin server is an onion service but Tor is disabled. Ignoring...");
						return null;
					}

					if (Global.Config.Network == Network.Main && payjoinEndPointUri.Scheme != Uri.UriSchemeHttps)
					{
						Logger.LogWarning("PayJoin server is not exposed as an onion service nor https. Ignoring...");
						return null;
					}
				}

				IHttpClient httpClient = Global.ExternalHttpClientFactory.NewHttpClient(() => payjoinEndPointUri, isolateStream: false);
				return new PayjoinClient(payjoinEndPointUri, httpClient);
			}

			return null;
		}

		public void ValidatePayjoinEndPoint(IValidationErrors errors)
		{
			if (!string.IsNullOrWhiteSpace(PayjoinEndPoint) && !Uri.IsWellFormedUriString(PayjoinEndPoint, UriKind.Absolute))
			{
				errors.Add(ErrorSeverity.Error, "Invalid url.");
			}
		}

		protected override void OnAddressPaste(BitcoinUrlBuilder url)
		{
			base.OnAddressPaste(url);

			if (url.UnknowParameters.TryGetValue("pj", out var endPoint))
			{
				if (!Wallet.KeyManager.IsWatchOnly)
				{
					PayjoinEndPoint = endPoint;
					return;
				}
				NotificationHelpers.Warning("PayJoin is not allowed here.");
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
