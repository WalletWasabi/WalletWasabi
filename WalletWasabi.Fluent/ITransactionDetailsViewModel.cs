using NBitcoin;

namespace WalletWasabi.Fluent;

public interface ITransactionDetailsViewModel
{
	public IObservable<Money> Amount { get; }
}
