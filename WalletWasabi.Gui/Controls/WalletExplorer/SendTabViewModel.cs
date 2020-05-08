using NBitcoin;
using NBitcoin.Payment;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
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
using WalletWasabi.Interfaces;

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
			BuildTransactionResult result = await Task.Run(() => Wallet.BuildTransaction(Password, payments, feeStrategy, allowUnconfirmed: true, allowedInputs: allowedInputs, GetPayjoinClient(), GetSigner()));

			MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusType.SigningTransaction);
			SmartTransaction signedTransaction = result.Transaction;
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
				return new PayjoinClient(payjoinEndPointUri, Global.TorManager.TorSocks5EndPoint);
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

		private IPsbtSigner GetSigner()
		{
			return !Wallet.KeyManager.IsHardwareWallet
				? null
				: new HwiPsbtSigner(new HwiClient(Global.Network), b => IsHardwareBusy = b);
		}

		class HwiPsbtSigner : IPsbtSigner
		{
			private readonly HwiClient _hwiClient;
			private readonly Action<bool> _hardwareBusyModifier;

			public HwiPsbtSigner(HwiClient hwiClient, Action<bool> hardwareBusyModifier)
			{
				_hwiClient = hwiClient;
				_hardwareBusyModifier = hardwareBusyModifier;
			}

			public async Task<PSBT> TrySign(PSBT psbt, KeyManager keyManager, CancellationToken cancellationToken)
			{
				try
				{
					if (!keyManager.IsHardwareWallet)
					{
						return null;
					}

					_hardwareBusyModifier.Invoke(true);
					MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusType
						.AcquiringSignatureFromHardwareWallet);
					try
					{
						return await _hwiClient.SignTxAsync(keyManager.MasterFingerprint.Value, psbt, false,
							cancellationToken);
					}
					catch (HwiException)
					{
						await PinPadViewModel.UnlockAsync();
						return await _hwiClient.SignTxAsync(keyManager.MasterFingerprint.Value, psbt, false,
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
					_hardwareBusyModifier.Invoke(false);
				}
			}
		}
	}
}
