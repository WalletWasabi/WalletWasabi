using DynamicData;
using NBitcoin;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.Models.UI;

#nullable disable

public class NullWalletRepository : IWalletRepository
{
	public NullWalletRepository()
	{
		Wallets = Array.Empty<IWalletModel>()
					   .AsObservableChangeSet(x => x.Name);
	}

	public IObservable<IChangeSet<IWalletModel, string>> Wallets { get; }

	public IWalletModel? DefaultWallet => null;

	public bool HasWallet => throw new NotImplementedException();

	public Task<IWalletSettingsModel> CreateNewWalletAsync(string walletName, string password, Mnemonic mnemonic)
	{
		return Task.FromResult(default(IWalletSettingsModel));
	}

	public IWalletModel SaveWallet(IWalletSettingsModel walletSettings)
	{
		return default;
	}

	public Task<IWalletSettingsModel> RecoverWalletAsync(string walletName, string password, Mnemonic mnemonic, int minGapLimit)
	{
		return Task.FromResult(default(IWalletSettingsModel));
	}
}
