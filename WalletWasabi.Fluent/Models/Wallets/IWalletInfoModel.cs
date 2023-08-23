using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletInfoModel
{
	string? ExtendedAccountPrivateKey { get; }

	string? ExtendedMasterPrivateKey { get; }

	string? ExtendedMasterZprv { get; }

	string? MasterKeyFingerprint { get; }

	string SegWitAccountKeyPath { get; }

	string SegWitExtendedAccountPublicKey { get; }

	string TaprootAccountKeyPath { get; }

	string? TaprootExtendedAccountPublicKey { get; }

	WpkhOutputDescriptorHelper.WpkhDescriptors? WpkhOutputDescriptors { get; }
}
