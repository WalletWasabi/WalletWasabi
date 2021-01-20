using Nito.AsyncEx;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;
using WalletWasabi.Tor.Socks5.Pool;
using WalletWasabi.Tor.Socks5.Utils;

namespace WalletWasabi.Tor.Socks5
{
	/// <summary>
	/// The pool represents a set of multiple TCP connections to Tor SOCKS5 endpoint that are stored in <see cref="TorPoolItem"/>s.
	/// </summary>
	public class TorSocks5ClientPool : IDisposable
	{
		/// <summary>Maximum number of <see cref="TorPoolItem"/>s per URI host.</summary>
		/// <remarks>This parameter affects maximum parallelization for given URI host.</remarks>
		public const int MaxPoolItemsPerHost = 10;

		/// <summary>
		/// Delegate method for creating a new pool item.
		/// </summary>
		/// <param name="requestUri">HTTP request URI for which to create <see cref="IPoolItem"/> instance.</param>
		/// <param name="isolateStream"><c>true</c> if a new Tor circuit is required for this HTTP request.</param>
		/// <param name="token">Cancellation token to cancel the asynchronous operation.</param>
		/// <returns>New pool item.</returns>
		public delegate Task<IPoolItem> CreateNewPoolItemDelegateAsync(Uri requestUri, bool isolateStream, CancellationToken token = default);

		/// <summary>
		/// Creates a new instance of the object.
		/// </summary>
		public static TorSocks5ClientPool Create(EndPoint endpoint)
		{
			ClearnetHttpClient httpClient = new();
			TorPoolItemManager torPoolItemManager = new(MaxPoolItemsPerHost); // Object ownership is transfered. Do not dispose here.
			TorSocks5ClientFactory torSocks5ClientFactory = new(endpoint);

			return new TorSocks5ClientPool(httpClient, torPoolItemManager, torSocks5ClientFactory.EstablishConnectionAsync);
		}

		/// <summary>
		/// Creates a new instance of the object.
		/// </summary>
		/// <remarks>
		/// This object is responsible for disposing of <paramref name="poolItemManager"/>.
		/// </remarks>
		public TorSocks5ClientPool(
			IRelativeHttpClient httpClient,
			TorPoolItemManager poolItemManager,
			CreateNewPoolItemDelegateAsync newPoolItemCreator)
		{
			ClearnetHttpClient = httpClient;
			PoolItemManager = poolItemManager;
			NewPoolItemCreator = newPoolItemCreator;
		}

		private bool _disposedValue;

		/// <remarks>Lock object required for the combination of <see cref="IPoolItem"/> selection or creation in <see cref="ObtainPoolItemAsync(HttpRequestMessage, bool, CancellationToken)"/>.</remarks>
		private AsyncLock ObtainPoolItemAsyncLock { get; } = new AsyncLock();

		private TorPoolItemManager PoolItemManager { get; }

		/// <inheritdoc cref="CreateNewPoolItemDelegateAsync"/>
		private CreateNewPoolItemDelegateAsync NewPoolItemCreator { get; }
		private IRelativeHttpClient ClearnetHttpClient { get; }

		/// <summary>TODO: Add locking and wrap in a class.</summary>
		public DateTimeOffset? TorDoesntWorkSince { get; private set; }

		/// <summary>TODO: Add locking.</summary>
		public Exception? LatestTorException { get; private set; } = null;

		/// <summary>
		/// This method is called when an HTTP(s) request fails for some reason.
		/// <para>The information is stored to allow <see cref="TorMonitor"/> to restart Tor as deemed fit.</para>
		/// </summary>
		/// <param name="e">Tor exception.</param>
		private void OnTorRequestFailed(Exception e)
		{
			if (TorDoesntWorkSince is null)
			{
				TorDoesntWorkSince = DateTimeOffset.UtcNow;
			}

			LatestTorException = e;
		}

		/// <summary>
		/// Sends an HTTP(s) request.
		/// <para>HTTP(s) requests with loopback destination after forwarded to <see cref="ClearnetHttpClient"/> and that's it.</para>
		/// <para>When a new non-loopback HTTP(s) request comes, <see cref="TorPoolItem"/> (or rather the TCP connection wrapped inside) is selected using these rules:
		/// <list type="number">
		/// <item>An unused <see cref="TorPoolItem"/> is selected, if it exists.</item>
		/// <item>A new <see cref="TorPoolItem"/> is added to the pool, if it would not exceed the maximum limit on the number of connections to Tor SOCKS5 endpoint.</item>
		/// <item>Keep waiting 1 second until any of the previous rules cannot be used.</item>
		/// </list>
		/// </para>
		/// <para><see cref="ObtainPoolItemAsyncLock"/> is acquired only for <see cref="TorPoolItem"/> selection.</para>
		/// </summary>
		/// <param name="request">HTTP request message to send.</param>
		/// <param name="isolateStream"><c>true</c> value is only available for Tor HTTP client to use a new Tor circuit, <c>false</c> otherwise.</param>
		/// <param name="cancellationToken">Cancellation token to cancel the asynchronous operation.</param>
		/// <see cref="HttpRequestException">Caller is supposed to catch this exception so that handling of different <see cref="IHttpClient"/> implementations is correct.</see>
		public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool isolateStream, CancellationToken cancellationToken = default)
		{
			// Connecting to loopback's URIs cannot be done via Tor.
			if (request.RequestUri!.IsLoopback)
			{
				return await ClearnetHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
			}

			int i = 0;
			int attemptsNo = 3;
			IPoolItem? poolItem = null;

			try
			{
				do
				{
					i++;
					poolItem = await ObtainPoolItemAsync(request, isolateStream, cancellationToken).ConfigureAwait(false);
					IPoolItem? itemToDispose = poolItem;

					try
					{
						Logger.LogTrace($"['{poolItem}'][Attempt #{i}] About to send request.");
						HttpResponseMessage response = await SendCoreAsync(poolItem.GetTransportStream(), request, cancellationToken).ConfigureAwait(false);

						// Client works OK, no need to dispose.
						itemToDispose = null;

						// Let others use the client.
						PoolItemState state = poolItem.Unreserve();
						Logger.LogTrace($"['{poolItem}'][Attempt #{i}] Unreserve. State is: '{state}'.");

						TorDoesntWorkSince = null;
						LatestTorException = null;

						return response;
					}
					catch (IOException e)
					{
						// NetworkStream may throw IOException.
						TorConnectionException innerException = new($"Failed to read/write HTTP(s) request.", e);
						throw new HttpRequestException("Failed to handle the HTTP request via Tor.", innerException);
					}
					catch (TorConnectCommandFailedException e) when (e.RepField == RepField.TtlExpired)
					{
						// If we get TTL Expired error then wait and retry again linux often does this.
						Logger.LogTrace($"['{poolItem}'] TTL exception occurred.", e);

						await Task.Delay(3000, cancellationToken).ConfigureAwait(false);

						if (i == attemptsNo)
						{
							Logger.LogDebug($"['{poolItem}'] All {attemptsNo} attempts failed.");
							throw new HttpRequestException("Failed to handle the HTTP request via Tor.", e);
						}
					}
					catch (SocketException e) when (e.ErrorCode == (int)SocketError.ConnectionRefused)
					{
						Logger.LogTrace(e);
						TorConnectionException innerException = new("Connection was refused.", e);
						throw new HttpRequestException("Failed to handle the HTTP request via Tor.", innerException);
					}
					catch (Exception e)
					{
						Logger.LogTrace(e);
						throw;
					}
					finally
					{
						(itemToDispose as IDisposable)?.Dispose();
					}
				} while (i < attemptsNo);
			}
			catch (Exception e)
			{
				Logger.LogTrace($"[{poolItem}] Request failed with exception", e);
				OnTorRequestFailed(e);
				throw;
			}

			throw new NotImplementedException("This should never happen.");
		}

		private async Task<IPoolItem> ObtainPoolItemAsync(HttpRequestMessage request, bool isolateStream, CancellationToken token)
		{
			Logger.LogTrace($"> request='{request.RequestUri}', isolateStream={isolateStream}");

			string host = GetRequestHost(request);

			do
			{
				using (await ObtainPoolItemAsyncLock.LockAsync(token).ConfigureAwait(false))
				{
					bool canBeAdded = PoolItemManager.GetPoolItem(host, isolateStream, out IPoolItem? poolItem);

					if (poolItem is { })
					{
						Logger.LogTrace($"[OLD {poolItem}]['{request.RequestUri}'] Re-use existing Tor SOCKS5 connection.");
						return poolItem;
					}

					if (canBeAdded)
					{
						poolItem = await CreateNewPoolItemNoLockAsync(request, isolateStream, token).ConfigureAwait(false);

						if (poolItem is { })
						{
							Logger.LogTrace($"[NEW {poolItem}]['{request.RequestUri}'] Using new Tor SOCKS5 connection.");
							return poolItem;
						}
					}
				}

				Logger.LogTrace("Wait 1s for a free pool item.");
				await Task.Delay(1000, token).ConfigureAwait(false);
			} while (true);
		}

		/// <remarks>Caller is responsible for acquiring <see cref="ObtainPoolItemAsyncLock"/>.</remarks>
		private async Task<IPoolItem?> CreateNewPoolItemNoLockAsync(HttpRequestMessage request, bool isolateStream, CancellationToken token)
		{
			IPoolItem? poolItem = null;
			string host = GetRequestHost(request);

			try
			{
				poolItem = await NewPoolItemCreator.Invoke(request.RequestUri!, isolateStream, token).ConfigureAwait(false);
				Logger.LogTrace($"[NEW {poolItem}]['{request.RequestUri}'] Created new Tor SOCKS5 connection.");
				PoolItemManager.TryAddPoolItem(host, poolItem);
			}
			catch (TorException e)
			{
				Logger.LogDebug($"['{host}'][ERROR] Failed to create a new pool item.", e);
				throw;
			}
			catch (Exception e)
			{
				Logger.LogTrace($"['{host}'][EXCEPTION] {e}");
				throw;
			}

			Logger.LogTrace($"< poolItem='{poolItem}'; Context: existing hostItems = {string.Join(',', PoolItemManager.GetItemsCopy(host).Select(x => x.ToString()).ToArray())}.");
			return poolItem;
		}

		private async static Task<HttpResponseMessage> SendCoreAsync(Stream transportStream, HttpRequestMessage request, CancellationToken token = default)
		{
			TorHttpRequestPreprocessor.Preprocess(request);
			string requestString = await request.ToHttpStringAsync(token).ConfigureAwait(false);
			byte[] bytes = Encoding.UTF8.GetBytes(requestString);

			await transportStream.WriteAsync(bytes.AsMemory(0, bytes.Length), token).ConfigureAwait(false);
			await transportStream.FlushAsync(token).ConfigureAwait(false);

			return await HttpResponseMessageExtensions.CreateNewAsync(transportStream, request.Method).ConfigureAwait(false);
		}

		private static string GetRequestHost(HttpRequestMessage request)
		{
			return Guard.NotNullOrEmptyOrWhitespace(nameof(request.RequestUri.DnsSafeHost), request.RequestUri!.DnsSafeHost, trim: true);
		}

		/// <summary>
		/// <list type="bullet">
		/// <item>Unmanaged resources need to be released regardless of the value of the <paramref name="disposing"/> parameter.</item>
		/// <item>Managed resources need to be released if the value of <paramref name="disposing"/> is <c>true</c>.</item>
		/// </list>
		/// </summary>
		/// <param name="disposing">
		/// Indicates whether the method call comes from a <see cref="Dispose()"/> method
		/// (its value is <c>true</c>) or from a finalizer (its value is <c>false</c>).
		/// </param>
		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					PoolItemManager.Dispose();
				}
				_disposedValue = true;
			}
		}

		/// <summary>
		/// Do not change this code.
		/// </summary>
		public void Dispose()
		{
			// Dispose of unmanaged resources.
			Dispose(true);

			// Suppress finalization.
			GC.SuppressFinalize(this);
		}
	}
}