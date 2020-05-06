using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Payment;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models.StatusBarStatuses;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Logging;
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
			this.ValidateProperty(x => x.PayjoinEndPoint, ValidatePayjoinEndPoint);
		}

		public override string DoButtonText => "Send Transaction";
		public override string DoingButtonText => "Sending Transaction...";

		protected override async Task BuildTransaction(string password, PaymentIntent payments, FeeStrategy feeStrategy, bool allowUnconfirmed = false, IEnumerable<OutPoint> allowedInputs = null)
		{
			var pjClient = GetPayjoinClient();
			BuildTransactionResult result = await Task.Run(() => Wallet.BuildTransaction(Password, payments, feeStrategy, allowUnconfirmed: true, allowedInputs: allowedInputs, pjClient));

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

					async Task<PSBT> SignWithHWW(PSBT psbt)
					{
						try
						{
							return await client.SignTxAsync(Wallet.KeyManager.MasterFingerprint.Value, psbt, false, cts.Token);
						}
						catch (HwiException)
						{
							await PinPadViewModel.UnlockAsync();
							return await client.SignTxAsync(Wallet.KeyManager.MasterFingerprint.Value, psbt, false, cts.Token);
						}
					}

					PSBT signedPsbt = await SignWithHWW(result.Psbt);
					if (pjClient != null)
					{
						var signedPayjoinPsbt = await pjClient.TryNegotiatePayjoin(SignWithHWW, signedPsbt,
							Wallet.KeyManager);
						if (signedPayjoinPsbt != null)
						{
							//TODO: Schedule signedPsbt to be broadcast in 2 mins
							signedPsbt = signedPayjoinPsbt;
						}
					}
					if (!signedPsbt.IsAllFinalized())
					{
						signedPsbt.Finalize();
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
				if (Global.Config.UseTor)
				{
					return new PayjoinClient(payjoinEndPointUri, Global.TorManager.TorSocks5EndPoint);
				}
				if (payjoinEndPointUri.DnsSafeHost.EndsWith(".onion", StringComparison.OrdinalIgnoreCase))
				{
					Logger.LogWarning("Payjoin server is a hidden service but Tor is disabled. Ignoring...");
					return null;
				}
				//TODO: Use an IHttpClientFactory to construct the HttpClient
				return new PayjoinClient(payjoinEndPointUri, new HttpClient());
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
				if (!Wallet.KeyManager.IsWatchOnly || Wallet.KeyManager.IsHardwareWallet)
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
