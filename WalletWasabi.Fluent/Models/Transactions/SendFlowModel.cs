using NBitcoin;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Transactions;

public record SendFlowModel
{
	private SendFlowModel(Wallet wallet, ICoinsView availableCoins, ICoinListModel coinListModel)
	{
		Wallet = wallet;
		AvailableCoins = availableCoins;
		CoinList = coinListModel;
	}

	/// <summary>Regular Send Flow. Uses all wallet coins</summary>
	public SendFlowModel(Wallet wallet, IWalletModel walletModel):
		this(wallet, wallet.Coins, walletModel.Coins)
	{
	}

	/// <summary>Manual Control Send Flow. Uses only the specified coins.</summary>
	public SendFlowModel(Wallet wallet, IWalletModel walletModel, IEnumerable<SmartCoin> coins):
		this(wallet, new CoinsView(coins), new UserSelectionCoinListModel(wallet, walletModel, coins.ToArray()))
	{
	}

	public Wallet Wallet { get; }

	public ICoinsView AvailableCoins { get; }

	public ICoinListModel CoinList { get; }

	public TransactionInfo? TransactionInfo { get; init; } = null;

	public decimal AvailableAmountBtc => AvailableAmount.ToDecimal(MoneyUnit.BTC);

	public Money AvailableAmount => AvailableCoins.TotalAmount();

	public bool IsManual => AvailableCoins.TotalAmount() != Wallet.Coins.TotalAmount();

	public IEnumerable<(LabelsArray Labels, ICoinsView Coins)> GetPockets() => AvailableCoins.GetPockets(Wallet.AnonScoreTarget);
}
