using NBitcoin;
using ReactiveUI;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class AmountProvider : ReactiveObject
{
	private readonly WasabiSynchronizer _synchronizer;
	[AutoNotify] private decimal _usdExchangeRate;

	public AmountProvider(WasabiSynchronizer synchronizer)
	{
		_synchronizer = synchronizer;
		BtcToUsdExchangeRates = this.WhenAnyValue(provider => provider._synchronizer.UsdExchangeRate);

		BtcToUsdExchangeRates.BindTo(this, x => x.UsdExchangeRate);
	}

	public IObservable<decimal> BtcToUsdExchangeRates { get; }

	public Amount Create(Money? money)
	{
		return new Amount(money ?? Money.Zero, this);
	}

	public Amount CreateFromBtc(decimal? btcAmount)
	{
		if (btcAmount is null)
		{
			return Create(Money.Zero);
		}

		return Create(new Money(btcAmount.Value, MoneyUnit.BTC));
	}

	public Amount CreateFromUsd(decimal? usdAmount)
	{
		if (usdAmount is null || UsdExchangeRate == 0)
		{
			return Create(Money.Zero);
		}

		var btcAmount = usdAmount.Value / UsdExchangeRate;
		return CreateFromBtc(btcAmount);
	}
}
