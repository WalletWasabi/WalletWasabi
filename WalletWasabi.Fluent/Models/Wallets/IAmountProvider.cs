using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IAmountProvider
{
	public Amount Create(Money? money);
	public IObservable<decimal> BtcToUsdExchangeRates { get; }
}
