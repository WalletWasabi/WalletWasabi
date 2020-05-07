using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Payment;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
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

		public string PayjoinEndPoint
		{
			get => _payjoinEndPoint;
			set => this.RaiseAndSetIfChanged(ref _payjoinEndPoint, value);
		}

		private Func<PSBT, CancellationToken, Task<PSBT>> GetSigner()
		{
			if (!Wallet.KeyManager.IsHardwareWallet)
			{
				return null;
			}

			var hwiClient = new HwiClient(Global.Network);
			return async (psbt, cancellationToken) =>
			{
				try
				{
					IsHardwareBusy = true;
					MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusType
						.AcquiringSignatureFromHardwareWallet);
					try
					{
						return await hwiClient.SignTxAsync(Wallet.KeyManager.MasterFingerprint.Value, psbt, false,
							cancellationToken);
					}
					catch (HwiException)
					{
						await PinPadViewModel.UnlockAsync();
						return await hwiClient.SignTxAsync(Wallet.KeyManager.MasterFingerprint.Value, psbt, false,
							cancellationToken);
					}
				}
				catch
				{
					return null;
				}
				finally
				{
					MainWindowViewModel.Instance.StatusBar.TryRemoveStatus(StatusType
						.AcquiringSignatureFromHardwareWallet);
					IsHardwareBusy = false;
				}
			};
		}

		protected override async Task BuildTransaction(string password, PaymentIntent payments, FeeStrategy feeStrategy,
			bool allowUnconfirmed = false, IEnumerable<OutPoint> allowedInputs = null)
		{
			var pjClient = GetPayjoinClient();
			BuildTransactionResult result = await Task.Run(() => Wallet.BuildTransaction(Password, payments,
				feeStrategy, allowUnconfirmed: true, allowedInputs: allowedInputs, pjClient, GetSigner()));

			MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusType.SigningTransaction);
			SmartTransaction signedTransaction = result.Transaction;

			MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusType.BroadcastingTransaction);
			await Task.Run(async () => await Global.TransactionBroadcaster.SendTransactionAsync(signedTransaction));

			ResetUi();
		}

		private IPayjoinClient GetPayjoinClient()
		{
			if (!string.IsNullOrWhiteSpace(PayjoinEndPoint) &&
			    Uri.IsWellFormedUriString(PayjoinEndPoint, UriKind.Absolute))
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

				// TODO: Use an IHttpClientFactory to construct the HttpClient
				if (Global.Config.Network == Network.RegTest)
				{
					HttpClientHandler clientHandler = new HttpClientHandler();
					clientHandler.ServerCertificateCustomValidationCallback =
						(sender, cert, chain, sslPolicyErrors) => true;

					return new PayjoinClient(payjoinEndPointUri, new HttpClient(clientHandler));
				}

				return new PayjoinClient(payjoinEndPointUri, new HttpClient());
			}

			return null;
		}

		public void ValidatePayjoinEndPoint(IValidationErrors errors)
		{
			if (!string.IsNullOrWhiteSpace(PayjoinEndPoint) &&
			    !Uri.IsWellFormedUriString(PayjoinEndPoint, UriKind.Absolute))
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