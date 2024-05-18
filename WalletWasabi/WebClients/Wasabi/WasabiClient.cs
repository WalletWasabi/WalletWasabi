using NBitcoin;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Services;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;

namespace WalletWasabi.WebClients.Wasabi;

public class WasabiClient
{
	[Obsolete("For legacy Tor HTTP implementation support. Use the other constructor in new code.")]
	public WasabiClient(IHttpClient httpClient)
	{
		HttpClient = httpClient;
	}

	/// <param name="httpClient">HTTP client set to use clearnet or with proper <see cref="WebProxy"/> set to connect to Tor over SOCKS5.</param>
	public WasabiClient(HttpClient httpClient)
	{
		HttpClient = httpClient;
	}

	/// <summary>Either <see cref="IHttpClient"/> (Wasabi custom implementation), or <see cref="HttpClient"/> (.NET type).</summary>
	private object? HttpClient { get; }

	public static Dictionary<uint256, Transaction> TransactionCache { get; } = new();
	private static Queue<uint256> TransactionIdQueue { get; } = new();
	public static object TransactionCacheLock { get; } = new();
	public static ushort ApiVersion { get; private set; } = ushort.Parse(Helpers.Constants.BackendMajorVersion);

	/// <remarks>
	/// Throws OperationCancelledException if <paramref name="cancel"/> is set.
	/// </remarks>
	public async Task<SynchronizeResponse> GetSynchronizeAsync(uint256 bestKnownBlockHash, int count, EstimateSmartFeeMode? estimateMode = null, CancellationToken cancel = default)
	{
		string relativeUri = $"api/v{ApiVersion}/btc/batch/synchronize?bestKnownBlockHash={bestKnownBlockHash}&maxNumberOfFilters={count}";
		if (estimateMode is { })
		{
			relativeUri = $"{relativeUri}&estimateSmartFeeMode={estimateMode}";
		}

		using HttpResponseMessage response = await GetRequestAsync(relativeUri, cancel).ConfigureAwait(false);

		if (response.StatusCode != HttpStatusCode.OK)
		{
			await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
		}

		using HttpContent content = response.Content;
		var ret = await content.ReadAsJsonAsync<SynchronizeResponse>().ConfigureAwait(false);

		return ret;
	}

	public async Task<IEnumerable<Transaction>> GetTransactionsAsync(Network network, IEnumerable<uint256> txHashes, CancellationToken cancel)
	{
		var allTxs = new List<Transaction>();
		var txHashesToQuery = new List<uint256>();
		lock (TransactionCacheLock)
		{
			var cachedTxs = TransactionCache.Where(x => txHashes.Contains(x.Key));
			allTxs.AddRange(cachedTxs.Select(x => x.Value));
			txHashesToQuery.AddRange(txHashes.Except(cachedTxs.Select(x => x.Key)));
		}

		foreach (IEnumerable<uint256> chunk in txHashesToQuery.ChunkBy(10))
		{
			cancel.ThrowIfCancellationRequested();

			using HttpResponseMessage response = await GetRequestAsync(
				$"api/v{ApiVersion}/btc/blockchain/transaction-hexes?&transactionIds={string.Join("&transactionIds=", chunk.Select(x => x.ToString()))}",
				cancellationToken: cancel).ConfigureAwait(false);

			if (response.StatusCode != HttpStatusCode.OK)
			{
				await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
			}

			using HttpContent content = response.Content;
			var retString = await content.ReadAsJsonAsync<IEnumerable<string>>().ConfigureAwait(false);
			var ret = retString.Select(x => Transaction.Parse(x, network)).ToList();

			lock (TransactionCacheLock)
			{
				foreach (var tx in ret)
				{
					tx.PrecomputeHash(false, true);
					if (TransactionCache.TryAdd(tx.GetHash(), tx))
					{
						TransactionIdQueue.Enqueue(tx.GetHash());
						if (TransactionCache.Count > 1000) // No more than 1000 txs in cache
						{
							var toRemove = TransactionIdQueue.Dequeue();
							TransactionCache.Remove(toRemove);
						}
					}
				}
			}
			allTxs.AddRange(ret);
		}

		return allTxs.ToDependencyGraph().OrderByDependency();
	}

	public async Task BroadcastAsync(string hex)
	{
		using var content = new StringContent($"'{hex}'", Encoding.UTF8, "application/json");
		using HttpResponseMessage response = await PostRequestAsync( $"api/v{ApiVersion}/btc/blockchain/broadcast", content, CancellationToken.None)
			.ConfigureAwait(false);

		if (response.StatusCode != HttpStatusCode.OK)
		{
			await response.ThrowRequestExceptionFromContentAsync(CancellationToken.None).ConfigureAwait(false);
		}
	}

	public async Task BroadcastAsync(Transaction transaction)
	{
		await BroadcastAsync(transaction.ToHex()).ConfigureAwait(false);
	}

	public async Task BroadcastAsync(SmartTransaction transaction)
	{
		await BroadcastAsync(transaction.Transaction).ConfigureAwait(false);
	}

	public async Task<IEnumerable<uint256>> GetMempoolHashesAsync(CancellationToken cancel = default)
	{
		using HttpResponseMessage response = await GetRequestAsync(
			$"api/v{ApiVersion}/btc/blockchain/mempool-hashes",
			cancellationToken: cancel).ConfigureAwait(false);

		if (response.StatusCode != HttpStatusCode.OK)
		{
			await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
		}

		using HttpContent content = response.Content;
		var strings = await content.ReadAsJsonAsync<IEnumerable<string>>().ConfigureAwait(false);
		var ret = strings.Select(x => new uint256(x));

		return ret;
	}

	/// <summary>
	/// Gets mempool hashes, but strips the last x characters of each hash.
	/// </summary>
	/// <param name="compactness">1 to 64</param>
	public async Task<ISet<string>> GetMempoolHashesAsync(int compactness, CancellationToken cancel = default)
	{
		using HttpResponseMessage response = await GetRequestAsync(
			$"api/v{ApiVersion}/btc/blockchain/mempool-hashes?compactness={compactness}",
			cancellationToken: cancel).ConfigureAwait(false);

		if (response.StatusCode != HttpStatusCode.OK)
		{
			await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
		}

		using HttpContent content = response.Content;
		var strings = await content.ReadAsJsonAsync<ISet<string>>().ConfigureAwait(false);

		return strings;
	}

	public async Task<ushort> GetBackendMajorVersionAsync(CancellationToken cancel)
	{
		using HttpResponseMessage response = await GetRequestAsync("api/software/versions", cancel).ConfigureAwait(false);

		if (response.StatusCode != HttpStatusCode.OK)
		{
			await response.ThrowRequestExceptionFromContentAsync(cancel).ConfigureAwait(false);
		}

		using HttpContent content = response.Content;
		var resp = await content.ReadAsJsonAsync<VersionsResponse>().ConfigureAwait(false);

		return ushort.Parse(resp.BackendMajorVersion);
	}

	public async Task<UpdateManager.UpdateStatus> CheckUpdatesAsync(CancellationToken cancel)
	{
		var backendMajorVersion = await GetBackendMajorVersionAsync(cancel).ConfigureAwait(false);

		// If ClientSupportBackendVersionMin <= backend major <= ClientSupportBackendVersionMax, then our software is compatible.
		var backendCompatible = int.Parse(Helpers.Constants.ClientSupportBackendVersionMax) >= backendMajorVersion && backendMajorVersion >= int.Parse(Helpers.Constants.ClientSupportBackendVersionMin);
		var currentBackendMajorVersion = backendMajorVersion;

		if (backendCompatible)
		{
			// Only refresh if compatible.
			ApiVersion = currentBackendMajorVersion;
		}

		return new UpdateManager.UpdateStatus(backendCompatible);
	}

	private async Task<HttpResponseMessage> GetRequestAsync(string relativeUri, CancellationToken cancellationToken)
	{
		if (HttpClient is IHttpClient wwHttpClient)
		{
			return await wwHttpClient.SendAsync(HttpMethod.Get, relativeUri, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
		else if (HttpClient is HttpClient httpClient)
		{
			using HttpRequestMessage requestMessage = new(HttpMethod.Get, requestUri: relativeUri);
            return await httpClient.SendAsync(requestMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

		throw new NotSupportedException();
    }

	private async Task<HttpResponseMessage> PostRequestAsync(string relativeUri, HttpContent content, CancellationToken cancellationToken)
	{
		if (HttpClient is IHttpClient wwHttpClient)
		{
			return await wwHttpClient.SendAsync(HttpMethod.Post, relativeUri, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
		else if (HttpClient is HttpClient httpClient)
		{
			using HttpRequestMessage requestMessage = new(HttpMethod.Post, requestUri: relativeUri);
			requestMessage.Content = content;

            return await httpClient.SendAsync(requestMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

		throw new NotSupportedException();
    }
}
