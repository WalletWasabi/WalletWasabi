using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Currency;
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

	public Amount? Create(Money? money)
	{
		if (money is null)
		{
			return null;
		}

		return new Amount(money, this);
	}

	public Amount? CreateFromBtc(decimal? btcAmount)
	{
		if (btcAmount is null)
		{
			return null;
		}

		return Create(new Money(btcAmount.Value, MoneyUnit.BTC));
	}

	public Amount? CreateFromUsd(decimal? usdAmount)
	{
		if (usdAmount is null || UsdExchangeRate == 0)
		{
			return null;
		}

		var btcAmount = usdAmount.Value / UsdExchangeRate;
		return CreateFromBtc(btcAmount);
	}

	public Amount? CreateFromBtc(CurrencyValue value) => CreateFromValue(value, false);

	public Amount? CreateFromUsd(CurrencyValue value) => CreateFromValue(value, true);

	private Amount? CreateFromValue(CurrencyValue value, bool isUsd)
	{
		return (value, isUsd) switch
		{
			(CurrencyValue.Empty, _) => null,
			(CurrencyValue.Invalid, _) => Amount.Invalid,
			(CurrencyValue.Valid x, true) => CreateFromUsd(x.Value),
			(CurrencyValue.Valid x, false) => CreateFromBtc(x.Value),
			_ => throw new InvalidOperationException($"CurrencyValue not supported: {value}")
		};
	}
}
