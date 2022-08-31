using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels;

public partial class DebugCoinViewModel : ViewModelBase
{
	private readonly SmartCoin _coin;

	public DebugCoinViewModel(SmartCoin coin)
	{
		_coin = coin;

		Amount = _coin.Amount;

		Transaction = new DebugTransactionViewModel(coin.Transaction);

		if (coin.SpenderTransaction is { })
		{
			SpenderTransaction = new DebugTransactionViewModel(coin.SpenderTransaction);
		}
	}

	public Money Amount { get; private set; }

	public DebugTransactionViewModel Transaction { get; private set; }

	public DebugTransactionViewModel? SpenderTransaction { get; private set; }
}
