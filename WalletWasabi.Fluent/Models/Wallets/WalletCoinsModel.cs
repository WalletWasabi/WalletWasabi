using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class WalletCoinsModel(Wallet wallet, IWalletModel walletModel) : CoinListModel(wallet, walletModel)
{
	public async Task UpdateExcludedCoinsFromCoinjoinAsync(ICoinModel[] coinsToExclude)
	{
		await Task.Run(() =>
		{
			// TODO: To keep models in sync with business objects. Should be automatic.
			foreach (var coinModel in List.Items)
			{
				coinModel.IsExcludedFromCoinJoin = coinsToExclude.Any(x => x.IsSame(coinModel));
			}

			var outPoints = coinsToExclude.Select(x => x.GetSmartCoin().Outpoint).ToArray();
			Wallet.UpdateExcludedCoinsFromCoinJoin(outPoints);
		});
	}

	public List<ICoinModel> GetSpentCoins(BuildTransactionResult? transaction)
	{
		var coins = (transaction?.SpentCoins ?? new List<SmartCoin>()).ToList();
		return coins.Select(GetCoinModel).ToList();
	}

	public bool AreEnoughToCreateTransaction(TransactionInfo transactionInfo, IEnumerable<ICoinModel> coins)
	{
		return TransactionHelpers.TryBuildTransactionWithoutPrevTx(Wallet.KeyManager, transactionInfo, Wallet.Coins, coins.GetSmartCoins(), Wallet.Password, out _);
	}

	protected override Pocket[] GetPockets()
	{
		return Wallet.GetPockets().ToArray();
	}

	protected override ICoinModel[] GetCoins()
	{
		return Wallet.Coins.Select(GetCoinModel).ToArray();
	}
}
