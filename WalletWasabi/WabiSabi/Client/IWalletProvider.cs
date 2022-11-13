using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

public interface IWalletProvider
{
	Task<IEnumerable<IWallet>> GetWalletsAsync();
}
