using DynamicData;
using NBitcoin;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.Models.UI;

public class NullWalletList : IWalletListModel
{
	public NullWalletList()
	{
		Wallets = Array.Empty<IWalletModel>()
					   .AsObservableChangeSet(x => x.Name);
	}

	public IObservable<IChangeSet<IWalletModel, string>> Wallets { get; }

	public IWalletModel? DefaultWallet => null;

	public bool HasWallet => false;

	public Task<IWalletModel> RecoverWallet(string walletName, string password, Mnemonic mnemonic, int minGapLimit)
	{
		throw new NotImplementedException();
	}

	public void StoreLastSelectedWallet(IWalletModel wallet)
	{
	}
}
