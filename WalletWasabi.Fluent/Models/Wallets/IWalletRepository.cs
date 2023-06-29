using DynamicData;
using NBitcoin;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletRepository
{
	IObservable<IChangeSet<IWalletModel, string>> Wallets { get; }

	IWalletModel? DefaultWallet { get; }

	bool HasWallet { get; }

	Task<IWalletSettingsModel> RecoverWalletAsync(string walletName, string password, Mnemonic mnemonic, int minGapLimit);

	Task<IWalletSettingsModel> CreateNewWalletAsync(string walletName, string password, Mnemonic mnemonic);

	IWalletModel SaveWallet(IWalletSettingsModel walletSettings);

	void StoreLastSelectedWallet(IWalletModel wallet);
}
