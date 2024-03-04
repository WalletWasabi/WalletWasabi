using NBitcoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public interface IWalletViewModel
{
	void SelectTransaction(uint256 txid);
}
