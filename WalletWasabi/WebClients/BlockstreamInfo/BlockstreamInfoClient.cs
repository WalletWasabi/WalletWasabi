using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Tor.Http.Extensions;

namespace WalletWasabi.WebClients.BlockstreamInfo
{
	public class BlockstreamInfoClient : IMempoolSyncer
	{
		private bool _disposedValue;

		public BlockstreamInfoClient(Network network)
		{
			var baseUrl = "https://blockstream.info/";
			if (network == Network.Main)
			{
				baseUrl += "api/";
			}
			else if (network == Network.TestNet)
			{
				baseUrl += "testnet/api/";
			}
			else
			{
				throw new NotSupportedException("Specified network not supported.");
			}

			HttpClient = new HttpClient
			{
				BaseAddress = new Uri(baseUrl)
			};
			Network = network;
		}

		public HttpClient HttpClient { get; }
		public Network Network { get; }

		public async Task<Transaction> GetTransactionAsync(uint256 txid, CancellationToken cancel)
		{
			using var response = await HttpClient.GetAsync($"tx/{txid}/hex", cancel).ConfigureAwait(false);
			if (!response.IsSuccessStatusCode)
			{
				await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
			}
			using var content = response.Content;
			var hex = await content.ReadAsStringAsync(cancel).ConfigureAwait(false);
			return Transaction.Parse(hex, Network);
		}

		public async Task<IEnumerable<uint256>> GetMempoolTransactionIdsAsync(CancellationToken cancel)
		{
			using var response = await HttpClient.GetAsync("mempool/txids", cancel).ConfigureAwait(false);
			if (!response.IsSuccessStatusCode)
			{
				await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
			}
			using var content = response.Content;
			var stringIds = await content.ReadAsJsonAsync<IEnumerable<string>>().ConfigureAwait(false);
			return stringIds.Select(x => new uint256(x)).ToArray();
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
