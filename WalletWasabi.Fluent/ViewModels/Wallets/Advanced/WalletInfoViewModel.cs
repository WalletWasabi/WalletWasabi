using System;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced
{
	[NavigationMetaData(Title = "Wallet Info")]
	public partial class WalletInfoViewModel : RoutableViewModel
	{
		[AutoNotify] private bool _showSensitiveData;
		[AutoNotify] private string _showButtonText = "Show sensitive data";
		[AutoNotify] private string _lockIconString = "eye_show_regular";

		public WalletInfoViewModel(WalletViewModelBase walletViewModelBase)
		{
			var wallet = walletViewModelBase.Wallet;
			var network = wallet.Network;
			IsHardwareWallet = wallet.KeyManager.IsHardwareWallet;

			SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

			EnableCancel = !wallet.KeyManager.IsWatchOnly;

			NextCommand = ReactiveCommand.Create(() => Navigate().Clear());

			CancelCommand = ReactiveCommand.Create(() =>
			{
				ShowSensitiveData = !ShowSensitiveData;
				ShowButtonText = ShowSensitiveData ? "Hide sensitive data" : "Show sensitive data";
				LockIconString = ShowSensitiveData ? "eye_hide_regular" : "eye_show_regular";
			});

			if (!wallet.KeyManager.IsWatchOnly)
			{
				var secret = PasswordHelper.GetMasterExtKey(wallet.KeyManager, wallet.Kitchen.SaltSoup(), out _);

				ExtendedMasterPrivateKey = secret.GetWif(network).ToWif();
				ExtendedAccountPrivateKey = secret.Derive(wallet.KeyManager.AccountKeyPath).GetWif(network).ToWif();
				ExtendedMasterZprv = secret.ToZPrv(network);
				ExtendedAccountZprv = secret.Derive(wallet.KeyManager.AccountKeyPath).ToZPrv(network);
			}

			ExtendedAccountPublicKey = wallet.KeyManager.ExtPubKey.ToString(network);
			ExtendedAccountZpub = wallet.KeyManager.ExtPubKey.ToZpub(network);
			AccountKeyPath = $"m/{wallet.KeyManager.AccountKeyPath}";
			MasterKeyFingerprint = wallet.KeyManager.MasterFingerprint.ToString();
		}

		public string ExtendedAccountPublicKey { get; }

		public string ExtendedAccountZpub { get; }

		public string AccountKeyPath { get; }

		public string? MasterKeyFingerprint { get; }

		public string? ExtendedMasterPrivateKey { get; }

		public string? ExtendedAccountPrivateKey { get; }

		public string? ExtendedMasterZprv { get; }

		public string? ExtendedAccountZprv { get; }

		public bool IsHardwareWallet { get; }
	}
}
