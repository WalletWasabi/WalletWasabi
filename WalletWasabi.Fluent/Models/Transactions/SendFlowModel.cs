using System;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Models.Transactions;

public record SendFlowModel : IDisposable
{
	private readonly bool _ownsCoinList;

	private SendFlowModel(IWalletModel walletModel, ICoinsView availableCoins, ICoinListModel coinListModel, bool ownsCoinList, bool donate)
	{
		WalletModel = walletModel;
		AvailableCoins = availableCoins;
		CoinList = coinListModel;
		_ownsCoinList = ownsCoinList;
		Donate = donate;
	}

	/// <summary>Regular Send Flow. Uses all wallet coins</summary>
	public SendFlowModel(IWalletModel walletModel, bool donate = false):
		this(walletModel, walletModel.GetCoinsView(), walletModel.Coins, ownsCoinList: false, donate)
	{
	}

	/// <summary>Manual Control Send Flow. Uses only the specified coins.</summary>
#pragma warning disable CA2000 // Ownership of UserSelectionCoinListModel is transferred to this instance which will dispose it
	public SendFlowModel(IWalletModel walletModel, IEnumerable<SmartCoin> coins, IServices services, bool donate = false):
		this(walletModel, new CoinsView(coins), new UserSelectionCoinListModel(walletModel, coins.ToArray(), services), ownsCoinList: true, donate)
	{
	}
#pragma warning restore CA2000

	public IWalletModel WalletModel { get; }

	public ICoinsView AvailableCoins { get; }

	public ICoinListModel CoinList { get; }

	public TransactionInfo? TransactionInfo { get; init; } = null;

	public bool Donate { get; init; }

	public decimal AvailableAmountBtc => AvailableAmount.ToDecimal(MoneyUnit.BTC);

	public Money AvailableAmount => AvailableCoins.TotalAmount();

	public bool IsManual => AvailableCoins.TotalAmount() != WalletModel.GetCoinsView().TotalAmount();

	public Pocket[] GetPockets() =>
		AvailableCoins.GetPockets(WalletModel.AnonScoreTarget)
					  .Select(x => new Pocket(x))
		              .ToArray();

	public void Dispose()
	{
		if (_ownsCoinList && CoinList is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}
}
