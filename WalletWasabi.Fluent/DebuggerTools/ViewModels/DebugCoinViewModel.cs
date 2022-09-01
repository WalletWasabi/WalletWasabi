using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels;

public partial class DebugCoinViewModel : ViewModelBase
{
	private readonly SmartCoin _coin;

	public DebugCoinViewModel(SmartCoin coin)
	{
		_coin = coin;

		FirstSeen = coin.Transaction.FirstSeen;

		Amount = _coin.Amount;

		Confirmed = _coin.Confirmed;

		CoinJoinInProgress = _coin.CoinJoinInProgress;

		IsBanned = _coin.IsBanned;

		BannedUntilUtc = _coin.BannedUntilUtc;

		Height = _coin.Height;

		Transaction = new DebugTransactionViewModel(coin.Transaction);

		if (coin.SpenderTransaction is { })
		{
			SpenderTransaction = new DebugTransactionViewModel(coin.SpenderTransaction);
		}
	}

	public DateTimeOffset FirstSeen { get; private set; }

	public Money Amount { get; private set; }

	// TODO: HdPubKey

	public bool Confirmed { get; private set; }

	public bool CoinJoinInProgress { get; private set; }

	public bool IsBanned { get; private set; }

	public DateTimeOffset? BannedUntilUtc { get; private set; }

	public Height Height { get; private set; }

	public DebugTransactionViewModel Transaction { get; private set; }

	public DebugTransactionViewModel? SpenderTransaction { get; private set; }

	public uint256 TransactionId => Transaction.TransactionId;

	public uint256? SpenderTransactionId => SpenderTransaction?.TransactionId;
}
