using WalletWasabi.Extensions;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;
using static WalletWasabi.Blockchain.Keys.WpkhOutputDescriptorHelper;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class WalletInfoModel
{
	public WalletInfoModel(Wallet wallet)
	{
		var network = wallet.Network;
		if (!wallet.KeyManager.IsWatchOnly)
		{
			var secret = PasswordHelper.GetMasterExtKey(wallet.KeyManager, wallet.Password, out _);

			ExtendedMasterPrivateKey = secret.GetWif(network).ToWif();
			ExtendedAccountPrivateKey = secret.Derive(wallet.KeyManager.SegwitAccountKeyPath).GetWif(network).ToWif();
			ExtendedMasterZprv = secret.ToZPrv(network);

			// TODO: Should work for every type of wallet, temporarily disabling it.
			WpkhOutputDescriptors = wallet.KeyManager.GetOutputDescriptors(wallet.Password, network);
		}

		SegWitExtendedAccountPublicKey = wallet.KeyManager.SegwitExtPubKey.ToString(network);
		TaprootExtendedAccountPublicKey = wallet.KeyManager.TaprootExtPubKey?.ToString(network);

		SegWitAccountKeyPath = $"m/{wallet.KeyManager.SegwitAccountKeyPath}";
		TaprootAccountKeyPath = $"m/{wallet.KeyManager.TaprootAccountKeyPath}";
		MasterKeyFingerprint = wallet.KeyManager.MasterFingerprint?.ToString();
	}

	public string SegWitExtendedAccountPublicKey { get; }

	public string? TaprootExtendedAccountPublicKey { get; }

	public string SegWitAccountKeyPath { get; }

	public string TaprootAccountKeyPath { get; }

	public string? MasterKeyFingerprint { get; }

	public string? ExtendedMasterPrivateKey { get; }

	public string? ExtendedAccountPrivateKey { get; }

	public string? ExtendedMasterZprv { get; }

	public WpkhDescriptors? WpkhOutputDescriptors { get; }
}
