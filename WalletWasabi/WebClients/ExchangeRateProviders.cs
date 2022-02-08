using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.WebClients.BlockchainInfo;
using WalletWasabi.WebClients.Coinbase;
using WalletWasabi.WebClients.Bitstamp;
using WalletWasabi.WebClients.CoinGecko;
using WalletWasabi.WebClients.Gemini;
using WalletWasabi.WebClients.ItBit;
using System.Linq;

namespace WalletWasabi.WebClients;

public class ExchangeRateProvider : IExchangeRateProvider
{
	private readonly IExchangeRateProvider[] _exchangeRateProviders =
	{
			new BlockchainInfoExchangeRateProvider(),
			new BitstampExchangeRateProvider(),
			new CoinGeckoExchangeRateProvider(),
			new CoinbaseExchangeRateProvider(),
			new GeminiExchangeRateProvider(),
			new ItBitExchangeRateProvider()
		};

	public async Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync()
	{
		foreach (var provider in _exchangeRateProviders)
		{
			try
			{
				return await provider.GetExchangeRateAsync().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// Ignore it and try with the next one
				Logger.LogTrace(ex);
			}
		}
		return Enumerable.Empty<ExchangeRate>();
	}
}
