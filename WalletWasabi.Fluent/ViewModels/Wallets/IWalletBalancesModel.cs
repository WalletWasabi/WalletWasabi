using NBitcoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public interface IWalletBalancesModel
{
	IObservable<Money> BtcBalance { get; }
	IObservable<decimal> UsdBalance { get; }
	IObservable<decimal> ExchangeRate { get; }
	IObservable<bool> HasBalance { get; }
}
