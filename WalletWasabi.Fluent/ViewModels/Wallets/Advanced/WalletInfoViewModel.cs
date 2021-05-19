using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced
{
	[NavigationMetaData(Title = "Wallet Info")]
	public partial class WalletInfoViewModel : RoutableViewModel
	{
		public WalletInfoViewModel(Wallet wallet)
		{
			var network = wallet.Network;

			SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

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
	}
}
