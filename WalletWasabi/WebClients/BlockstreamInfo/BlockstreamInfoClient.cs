using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.Tor.Socks5.Pool;

namespace WalletWasabi.WebClients.BlockstreamInfo
{
	public class BlockstreamInfoClient : IDisposable
	{
		private bool _disposedValue;

		public BlockstreamInfoClient(Network network, TorHttpPool? pool = null)
		{
			if (pool is not null)
			{
				TorHttpClient = new TorHttpClient(new Uri(network == Network.TestNet ? "http://explorerzydxu5ecjrkwceayqybizmpjjznk5izmitf2modhcusuqlid.onion/testnet" : "http://explorerzydxu5ecjrkwceayqybizmpjjznk5izmitf2modhcusuqlid.onion"), pool);
			}
			else
			{
				HttpClient = new HttpClient();
				HttpClient.BaseAddress = new Uri(network == Network.TestNet ? "https://blockstream.info/testnet" : "https://blockstream.info");
			}
		}

		public TorHttpClient? TorHttpClient { get; }
		public HttpClient? HttpClient { get; }

		public async Task<BestFeeEstimates> GetFeeEstimatesAsync(CancellationToken cancel)
		{
			using HttpResponseMessage response = TorHttpClient is not null
				? await TorHttpClient.SendAsync(HttpMethod.Get, "api/fee-estimates", null, cancel).ConfigureAwait(false)
				: await HttpClient!.GetAsync("api/fee-estimates", cancel).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
			}

			var responseString = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
			var parsed = JsonDocument.Parse(responseString).RootElement;
			var myDic = new Dictionary<int, int>();
			foreach (var elem in parsed.EnumerateObject())
			{
				myDic.Add(int.Parse(elem.Name), (int)Math.Ceiling(elem.Value.GetDouble()));
			}

			return new BestFeeEstimates(EstimateSmartFeeMode.Conservative, myDic, isAccurate: true);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					HttpClient?.Dispose();
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
