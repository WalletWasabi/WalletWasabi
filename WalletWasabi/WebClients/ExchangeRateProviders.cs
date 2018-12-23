using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.WebClients.SmartBit;
using NBitcoin;
using WalletWasabi.Interfaces;
using WalletWasabi.WebClients.BlockchainInfo;
using WalletWasabi.WebClients.Coinbase;
using WalletWasabi.WebClients.Gemini;
using WalletWasabi.WebClients.ItBit;
using WalletWasabi.Logging;

namespace WalletWasabi.WebClients
{
	public class ExchangeRateProvider : IExchangeRateProvider
	{
		private readonly IExchangeRateProvider[] _exchangeRateProviders = {
			new SmartBitExchangeRateProvider(new SmartBitClient(Network.Main, disposeHandler: true)),
			new BlockchainInfoExchangeRateProvider(),
			new CoinbaseExchangeRateProvider(),
			new GeminiExchangeRateProvider(),
			new ItBitExchangeRateProvider()
		};

		public async Task<List<ExchangeRate>> GetExchangeRateAsync()
		{
			List<ExchangeRate> exchangeRates = null;

			foreach (var provider in _exchangeRateProviders)
			{
				try
				{
					exchangeRates = await provider.GetExchangeRateAsync();
					break;
				}
				catch (Exception ex)
				{
					// Ignore it and try with the next one
					Logger.LogTrace<ExchangeRateProvider>(ex);
				}
			}
			return exchangeRates;
		}
	}
}
