using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class UserSelectionCoinListModel(Wallet wallet, IWalletModel walletModel, SmartCoin[] selectedCoins) : CoinListModel(wallet, walletModel)
{
	protected override ICoinModel[] CreateCoinModels()
	{
		return selectedCoins.Select(CreateCoinModel).ToArray();
	}

	protected override Pocket[] GetPockets()
	{
		return
			new CoinsView(selectedCoins).GetPockets(WalletModel.Settings.AnonScoreTarget)
										.Select(x => new Pocket(x))
										.ToArray();
	}
}
