using System.Reactive;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels;

public partial class DebugCoinViewModel : ViewModelBase, IDisposable
{
	private readonly SmartCoin _coin;
	private readonly IObservable<Unit> _updateTrigger;

	public DebugCoinViewModel(SmartCoin coin, IObservable<Unit> updateTrigger)
	{
		_coin = coin;
		_updateTrigger = updateTrigger;

		Update();
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

	private void Update()
	{
		if (_coin.SpenderTransaction is { })
		{
			FirstSeen = _coin.SpenderTransaction.FirstSeen.LocalDateTime;
		}
		else
		{
			FirstSeen = _coin.Transaction.FirstSeen.LocalDateTime;
		}

		Amount = _coin.Amount;

		Confirmed = _coin.Confirmed;

		CoinJoinInProgress = _coin.CoinJoinInProgress;

		IsBanned = _coin.IsBanned;

		BannedUntilUtc = _coin.BannedUntilUtc;

		Height = _coin.Height;

		Transaction = new DebugTransactionViewModel(_coin.Transaction, _updateTrigger);

		if (_coin.SpenderTransaction is { })
		{
			SpenderTransaction = new DebugTransactionViewModel(_coin.SpenderTransaction, _updateTrigger);
		}
	}

	public void Dispose()
	{
		Transaction.Dispose();
		SpenderTransaction?.Dispose();
	}
}
