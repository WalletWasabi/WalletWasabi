using DynamicData;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial interface IWalletCoinsModel : ICoinListModel
{
	Task UpdateExcludedCoinsFromCoinjoinAsync(ICoinModel[] coinsToExclude);

	List<ICoinModel> GetSpentCoins(BuildTransactionResult? transaction);

	bool AreEnoughToCreateTransaction(TransactionInfo transactionInfo, IEnumerable<ICoinModel> coins);
}
