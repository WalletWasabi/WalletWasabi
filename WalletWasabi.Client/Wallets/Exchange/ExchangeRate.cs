using System;

namespace WalletWasabi.Client.Wallets.Exchange;

public record ExchangeRate(string Ticker, decimal Rate)
{
	public override string ToString() => $"{Rate} {Ticker}";
}

public static class ExchangeRateExtensions
{
	public static ExchangeRate? ForCurrency(this ExchangeRate[] exchangeRates, string currency) =>
		Array.Find(exchangeRates, x => x.Ticker == currency);
}
