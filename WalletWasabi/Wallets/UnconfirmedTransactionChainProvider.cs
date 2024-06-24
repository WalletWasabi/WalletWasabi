using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Wallets;

public class UnconfirmedTransactionChainProvider
{
	public UnconfirmedTransactionChainProvider(WasabiHttpClientFactory httpClientFactory)
	{
		HttpClient = httpClientFactory.NewHttpClient(() => new Uri("https://mempool.space/api/"), Tor.Socks5.Pool.Circuits.Mode.NewCircuitPerRequest);
	}

	private IHttpClient HttpClient { get; }

	public async Task<UnconfirmedTransactionChain?> FetchUnconfirmedTransactionChainAsync(uint256 txid, CancellationToken cancellationToken)
	{
		const int MaxAttempts = 3;

		for (int i = 0; i < MaxAttempts; i++)
		{
			try
			{
				using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(60));
				using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

				var response = await HttpClient.SendAsync(
					HttpMethod.Get,
					$"v1/cpfp/{txid}",
					null,
					linkedCts.Token).ConfigureAwait(false);

				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync(cancellationToken).ConfigureAwait(false);
				}

				return await response.Content.ReadAsJsonAsync<UnconfirmedTransactionChain>().ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				Logger.LogTrace("Request was cancelled by exiting the app.");
			}
			catch (HttpRequestException ex)
			{
				Logger.LogWarning($"Attempt: {i}. Failed to fetch transaction fee. {ex}");
			}
		}

		return null;
	}
}
