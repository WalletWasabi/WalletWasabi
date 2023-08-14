using DynamicData;
using System.Threading;
using NBitcoin;
using System.Threading.Tasks;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletRepository
{
	IObservable<IChangeSet<IWalletModel, string>> Wallets { get; }

	IWalletModel? DefaultWallet { get; }

	bool HasWallet { get; }

	string GetNextWalletName();

	Task<IWalletSettingsModel> NewWalletAsync(WalletCreationOptions options, CancellationToken? cancelToken = null);

	IWalletModel SaveWallet(IWalletSettingsModel walletSettings);

	(ErrorSeverity Severity, string Message)? ValidateWalletName(string walletName);

	IWalletModel? GetExistingWallet(HwiEnumerateEntry device);

	void StoreLastSelectedWallet(IWalletModel wallet);
}
