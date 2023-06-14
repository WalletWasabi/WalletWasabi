using DynamicData;
using NBitcoin;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletListModel
{
	IObservable<IChangeSet<IWalletModel, string>> Wallets { get; }

	IWalletModel? DefaultWallet { get; }

	bool HasWallet { get; }

	Task<IWalletModel> RecoverWallet(string walletName, string password, Mnemonic mnemonic, int minGapLimit);

	void StoreLastSelectedWallet(IWalletModel wallet);
}
