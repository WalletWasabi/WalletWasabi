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
	private SendFlowModel(Wallet wallet, ICoinsView availableCoins, ICoinListModel coinListModel, bool donate)
	{
		Wallet = wallet;
		AvailableCoins = availableCoins;
		CoinList = coinListModel;
		Donate = donate;
	}

	/// <summary>Regular Send Flow. Uses all wallet coins</summary>
	public SendFlowModel(Wallet wallet, IWalletModel walletModel, bool donate = false):
		this(wallet, wallet.Coins, walletModel.Coins, donate)
	{
	}

	/// <summary>Manual Control Send Flow. Uses only the specified coins.</summary>
	public SendFlowModel(Wallet wallet, IWalletModel walletModel, IEnumerable<SmartCoin> coins, bool donate = false):
		this(wallet, new CoinsView(coins), new UserSelectionCoinListModel(wallet, walletModel, coins.ToArray()), donate)
	{
	}

	public Wallet Wallet { get; }

	public ICoinsView AvailableCoins { get; }

	public ICoinListModel CoinList { get; }

	public TransactionInfo? TransactionInfo { get; init; } = null;

	public bool Donate { get; init; }

	public decimal AvailableAmountBtc => AvailableAmount.ToDecimal(MoneyUnit.BTC);

	public Money AvailableAmount => AvailableCoins.TotalAmount();

	public bool IsManual => AvailableCoins.TotalAmount() != Wallet.Coins.TotalAmount();

	public Pocket[] GetPockets() =>
		AvailableCoins.GetPockets(Wallet.AnonScoreTarget)
					  .Select(x => new Pocket(x))
		              .ToArray();
}
