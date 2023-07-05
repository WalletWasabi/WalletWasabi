using DynamicData;
using NBitcoin;
using System.Threading.Tasks;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletRepository
{
	IObservable<IChangeSet<IWalletModel, string>> Wallets { get; }

	IWalletModel? DefaultWallet { get; }

	bool HasWallet { get; }

	string GetNextWalletName();

	Task<IWalletSettingsModel> NewWalletAsync(WalletCreationOptions options);

	IWalletModel SaveWallet(IWalletSettingsModel walletSettings);

	(ErrorSeverity Severity, string Message)? ValidateWalletName(string walletName);

	void StoreLastSelectedWallet(IWalletModel wallet);
}
