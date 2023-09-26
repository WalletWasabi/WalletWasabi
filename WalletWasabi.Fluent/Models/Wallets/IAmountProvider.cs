using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IAmountProvider
{
	public Amount GetAmount(Money? money);
	public IObservable<decimal> BtcToUsdExchangeRates { get; }
}
