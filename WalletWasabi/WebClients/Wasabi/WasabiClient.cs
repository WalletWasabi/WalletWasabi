using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Bases;
using WalletWasabi.Models;

namespace WalletWasabi.WebClients.Wasabi
{
	public class WasabiClient : TorDisposableBase
	{
		/// <inheritdoc/>
		public WasabiClient(Func<Uri> baseUriAction, IPEndPoint torSocks5EndPoint) : base(baseUriAction, torSocks5EndPoint)
		{
		}

		/// <inheritdoc/>
		public WasabiClient(Uri baseUri, IPEndPoint torSocks5EndPoint) : base(baseUri, torSocks5EndPoint)
		{
		}

		#region batch

		/// <remarks>
		/// Throws OperationCancelledException if <paramref name="cancel"/> is set.
		/// </remarks>
		public async Task<SynchronizeResponse> GetSynchronizeAsync(uint256 bestKnownBlockHash, int count, EstimateSmartFeeMode? estimateMode = null, CancellationToken cancel = default)
		{
			string relativeUri = $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/batch/synchronize?bestKnownBlockHash={bestKnownBlockHash}&maxNumberOfFilters={count}";
			if (estimateMode != null)
			{
				relativeUri = $"{relativeUri}&estimateSmartFeeMode={estimateMode}";
			}

			using (var response = await TorClient.SendAndRetryAsync(HttpMethod.Get,
																	HttpStatusCode.OK,
																	relativeUri,
																	cancel: cancel).ConfigureAwait(false))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
				}

				using (HttpContent content = response.Content)
				{
					var ret = await content.ReadAsJsonAsync<SynchronizeResponse>().ConfigureAwait(false);
					return ret;
				}
			}
		}

		#endregion batch

		#region blockchain

		/// <remarks>
		/// Throws OperationCancelledException if <paramref name="cancel"/> is set.
		/// </remarks>
		public async Task<FiltersResponse> GetFiltersAsync(uint256 bestKnownBlockHash, int count, CancellationToken cancel = default)
		{
			using (var response = await TorClient.SendAndRetryAsync(HttpMethod.Get,
																	HttpStatusCode.OK,
																	$"/api/v{Helpers.Constants.BackendMajorVersion}/btc/blockchain/filters?bestKnownBlockHash={bestKnownBlockHash}&count={count}",
																	cancel: cancel).ConfigureAwait(false))
			{
				if (response.StatusCode == HttpStatusCode.NoContent)
				{
					return null;
				}
				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
				}

				using (HttpContent content = response.Content)
				{
					var ret = await content.ReadAsJsonAsync<FiltersResponse>().ConfigureAwait(false);
					return ret;
				}
			}
		}

		public async Task<IDictionary<int, FeeEstimationPair>> GetFeesAsync(params int[] confirmationTargets)
		{
			var confirmationTargetsString = string.Join(",", confirmationTargets);

			using (var response = await TorClient.SendAndRetryAsync(HttpMethod.Get, HttpStatusCode.OK, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/blockchain/fees/{confirmationTargetsString}").ConfigureAwait(false))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
				}

				using (HttpContent content = response.Content)
				{
					var ret = await content.ReadAsJsonAsync<IDictionary<int, FeeEstimationPair>>().ConfigureAwait(false);
					return ret;
				}
			}
		}

		public async Task BroadcastAsync(string hex)
		{
			using (var content = new StringContent($"'{hex}'", Encoding.UTF8, "application/json"))
			using (var response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/blockchain/broadcast", content).ConfigureAwait(false))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
				}
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
			using (var response = await TorClient.SendAndRetryAsync(HttpMethod.Get,
																	HttpStatusCode.OK,
																	$"/api/v{Helpers.Constants.BackendMajorVersion}/btc/blockchain/mempool-hashes",
																	cancel: cancel).ConfigureAwait(false))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
				}

				using (HttpContent content = response.Content)
				{
					var strings = await content.ReadAsJsonAsync<IEnumerable<string>>().ConfigureAwait(false);
					var ret = strings.Select(x => new uint256(x));
					return ret;
				}
			}
		}

		/// <summary>
		/// Gets mempool hashes, but strips the last x characters of each hash.
		/// </summary>
		/// <param name="compactness">1 to 64</param>
		public async Task<IEnumerable<string>> GetMempoolHashesAsync(int compactness, CancellationToken cancel = default)
		{
			using (var response = await TorClient.SendAndRetryAsync(HttpMethod.Get,
																	HttpStatusCode.OK,
																	$"/api/v{Helpers.Constants.BackendMajorVersion}/btc/blockchain/mempool-hashes?compactness={compactness}",
																	cancel: cancel).ConfigureAwait(false))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
				}

				using (HttpContent content = response.Content)
				{
					var strings = await content.ReadAsJsonAsync<IEnumerable<string>>().ConfigureAwait(false);
					return strings;
				}
			}
		}

		#endregion blockchain

		#region offchain

		public async Task<IEnumerable<ExchangeRate>> GetExchangeRatesAsync()
		{
			using (var response = await TorClient.SendAndRetryAsync(HttpMethod.Get, HttpStatusCode.OK, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/offchain/exchange-rates").ConfigureAwait(false))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
				}

				using (HttpContent content = response.Content)
				{
					var ret = await content.ReadAsJsonAsync<IEnumerable<ExchangeRate>>().ConfigureAwait(false);
					return ret;
				}
			}
		}

		#endregion offchain

		#region software

		public async Task<(Version ClientVersion, int BackendMajorVersion)> GetVersionsAsync(CancellationToken cancel)
		{
			using (var response = await TorClient.SendAndRetryAsync(HttpMethod.Get, HttpStatusCode.OK, "/api/software/versions", cancel: cancel).ConfigureAwait(false))
			{
				if (response.StatusCode == HttpStatusCode.NotFound)
				{
					// Meaning this things wasn't just yet implemented on the running server.
					return (new Version(0, 7), 1);
				}

				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
				}

				using (HttpContent content = response.Content)
				{
					var resp = await content.ReadAsJsonAsync<VersionsResponse>().ConfigureAwait(false);
					return (Version.Parse(resp.ClientVersion), int.Parse(resp.BackenMajordVersion));
				}
			}
		}

		public async Task<(bool backendCompatible, bool clientUpToDate)> CheckUpdatesAsync(CancellationToken cancel)
		{
			var versions = await GetVersionsAsync(cancel).ConfigureAwait(false);
			var clientUpToDate = Helpers.Constants.ClientVersion >= versions.ClientVersion; // If the client version locally is greater or equal to the backend's reported client version, then good.
			var backendCompatible = int.Parse(Helpers.Constants.BackendMajorVersion) == versions.BackendMajorVersion; // If the backend major and the client major equals, then our softwares are compatible.

			return (backendCompatible, clientUpToDate);
		}

		#endregion software
	}
}
