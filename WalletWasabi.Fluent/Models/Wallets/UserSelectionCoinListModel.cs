using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class UserSelectionCoinListModel(IWalletModel walletModel, SmartCoin[] selectedCoins, IServices services)
	: CoinListModel(walletModel, services)
{
	protected override CoinModel[] CreateCoinModels()
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
