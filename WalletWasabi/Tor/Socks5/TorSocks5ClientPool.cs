using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.Tor.Http.Models;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;
using WalletWasabi.Tor.Socks5.Pool;

namespace WalletWasabi.Tor.Socks5
{
	/// <summary>
	/// The pool represents a set of multiple TCP connections to Tor SOCKS5 endpoint that are stored in <see cref="PoolItem"/>s.
	/// <para>
	/// When a new HTTP(s) request comes, <see cref="PoolItem"/> (or rather the TCP connection wrapped inside) is selected using these rules:
	/// <list type="number">
	/// <item>An unused <see cref="PoolItem"/> is selected, if it exists.</item>
	/// <item>A new <see cref="PoolItem"/> is added to the pool, if it would not exceed the maximum limit on the number of connections to Tor SOCKS5 endpoint.</item>
	/// <item>Keep waiting 1 second until any of the previous rules cannot be used.</item>
	/// </list>
	/// </para>
	/// <para><see cref="ClientsAsyncLock"/> is acquired only for <see cref="PoolItem"/> selection.</para>
	/// </summary>
	public class TorSocks5ClientPool : IDisposable
	{
		/// <summary>Maximum number of <see cref="PoolItem"/>s per URI host.</summary>
		public const int MaxPoolItemsPerHost = 3;

		/// <summary>
		/// Creates a new instance of the object.
		/// </summary>
		public TorSocks5ClientPool(EndPoint endpoint)
		{
			ClearnetHttpClient = new ClearnetHttpClient();
			TorSocks5ClientFactory = new TorSocks5ClientFactory(endpoint);
			ClientsManager = new ClientsManager(MaxPoolItemsPerHost);
		}

		private bool _disposedValue;

		/// <remarks>Lock object to guard all access to <see cref="Clients"/>.</remarks>
		private AsyncLock ClientsAsyncLock { get; } = new AsyncLock();

		private ClientsManager ClientsManager { get; }

		private ClearnetHttpClient ClearnetHttpClient { get; }
		private TorSocks5ClientFactory TorSocks5ClientFactory { get; }

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
		/// Robust sending algorithm. TODO.
		/// </summary>
		/// <param name="request">TODO.</param>
		/// <param name="isolateStream">TODO.</param>
		/// <param name="token">TODO.</param>
		public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool isolateStream, CancellationToken token = default)
		{
			// Connecting to loopback's URIs cannot be done via Tor.
			if (request.RequestUri!.IsLoopback)
			{
				return await ClearnetHttpClient.SendAsync(request, token).ConfigureAwait(false);
			}

			int i = 0;
			int attemptsNo = 3;

			try
			{
				do
				{
					i++;
					IPoolItem poolItem = await ObtainFreePoolItemAsync(request, isolateStream, token).ConfigureAwait(false);
					IPoolItem? itemToDispose = poolItem;

					try
					{
						Logger.LogTrace($"['{poolItem}'] About to send request.");
						HttpResponseMessage response = await SendCoreAsync(poolItem.GetTransportStream(), request, token).ConfigureAwait(false);

						// Client works OK, no need to dispose.
						itemToDispose = null;

						// Let others use the client.
						var state = poolItem.Unreserve();
						Logger.LogTrace($"['{poolItem}'] Un-reserve. State is: '{state}'.");

						TorDoesntWorkSince = null;
						LatestTorException = null;

						return response;
					}
					catch (TorConnectCommandFailedException ex) when (ex.RepField == RepField.TtlExpired)
					{
						// If we get TTL Expired error then wait and retry again linux often does this.
						Logger.LogTrace(ex);

						await Task.Delay(1000, token).ConfigureAwait(false);

						if (i == attemptsNo)
						{
							Logger.LogDebug($"All {attemptsNo} attempts failed."); // TODO: Improve message.
							throw;
						}
					}
					catch (SocketException ex) when (ex.ErrorCode == (int)SocketError.ConnectionRefused)
					{
						Logger.LogTrace(ex);
						throw new TorConnectionException("Connection was refused.", ex);
					}
					finally
					{
						(itemToDispose as IDisposable)?.Dispose();
					}
				} while (i < attemptsNo);
			}
			catch (Exception ex)
			{
				OnTorRequestFailed(ex);
				throw;
			}

			throw new NotImplementedException("This should never happen.");
		}

		private async Task<IPoolItem> ObtainFreePoolItemAsync(HttpRequestMessage request, bool isolateStream, CancellationToken token)
		{
			do
			{
				using (await ClientsAsyncLock.LockAsync(token).ConfigureAwait(false))
				{
					IPoolItem? poolItem = await GetClientLockedAsync(request, isolateStream, token).ConfigureAwait(false);

					if (poolItem is { })
					{
						return poolItem;
					}
				}

				Logger.LogTrace("Wait 1s for a free pool item.");
				await Task.Delay(1000, token).ConfigureAwait(false);
			} while (true);
		}

		/// <remarks>Caller is responsible for acquiring <see cref="ClientsAsyncLock"/>.</remarks>
		private async Task<IPoolItem?> GetClientLockedAsync(HttpRequestMessage request, bool isolateStream, CancellationToken token)
		{
			string host = GetRequestHost(request);
			Logger.LogTrace($"> request='{request.RequestUri}', isolateStream={isolateStream}");

			(bool canBeAdded, IPoolItem? reservedItem) = ClientsManager.GetPoolItem(host, isolateStream);

			if (reservedItem is null)
			{
				if (!canBeAdded)
				{
					Logger.LogTrace($"['{host}'][NONE] No free pool item.");
				}
				else
				{
					try
					{
						bool useSsl = request.RequestUri!.Scheme == Uri.UriSchemeHttps;
						bool allowRecycling = !useSsl && !isolateStream;

						TorConnection newClient = await NewSocks5ClientAsync(request, useSsl, isolateStream, token).ConfigureAwait(false);
						reservedItem = new PoolItem(newClient, allowRecycling);

						Logger.LogTrace($"[NEW {reservedItem}]['{request.RequestUri}'] Created new Tor SOCKS5 connection.");

						ClientsManager.AddPoolItem(host, reservedItem);
					}
					catch (TorException e)
					{
						Logger.LogDebug($"['{host}'][ERROR] Failed to create a new pool item.");
						Logger.LogError(e);
					}
					catch (Exception e)
					{
						Logger.LogTrace($"['{host}'][EXCEPTION] {e}");
						throw;
					}
				}
			}
			else
			{
				Logger.LogTrace($"[OLD {reservedItem}]['{request.RequestUri}'] Re-use existing Tor SOCKS5 connection.");
			}

			Logger.LogTrace($"< reservedItem='{reservedItem}'; Context: existing hostItems = {string.Join(',', ClientsManager.GetItemsCopy(host).Select(x => x.ToString()).ToArray())}.");
			return reservedItem;
		}

		private async static Task<HttpResponseMessage> SendCoreAsync(Stream transportStream, HttpRequestMessage request, CancellationToken token = default)
		{
			request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

			// https://tools.ietf.org/html/rfc7230#section-3.3.2
			// A user agent SHOULD send a Content - Length in a request message when
			// no Transfer-Encoding is sent and the request method defines a meaning
			// for an enclosed payload body. For example, a Content - Length header
			// field is normally sent in a POST request even when the value is 0
			// (indicating an empty payload body). A user agent SHOULD NOT send a
			// Content - Length header field when the request message does not contain
			// a payload body and the method semantics do not anticipate such a
			// body.
			if (request.Method == HttpMethod.Post)
			{
				if (request.Headers.TransferEncoding.Count == 0)
				{
					if (request.Content is null)
					{
						request.Content = new ByteArrayContent(Array.Empty<byte>()); // dummy empty content
						request.Content.Headers.ContentLength = 0;
					}
					else
					{
						request.Content.Headers.ContentLength ??= (await request.Content.ReadAsStringAsync(token).ConfigureAwait(false)).Length;
					}
				}
			}

			string requestString = await request.ToHttpStringAsync().ConfigureAwait(false);

			var bytes = Encoding.UTF8.GetBytes(requestString);

			await transportStream.WriteAsync(bytes.AsMemory(0, bytes.Length), token).ConfigureAwait(false);
			await transportStream.FlushAsync(token).ConfigureAwait(false);

			return await HttpResponseMessageExtensions.CreateNewAsync(transportStream, request.Method).ConfigureAwait(false);
		}

		/// <inheritdoc cref="TorSocks5ClientFactory.MakeAsync(bool, string, int, bool, CancellationToken)"/>
		private Task<TorConnection> NewSocks5ClientAsync(HttpRequestMessage request, bool useSsl, bool isolateStream, CancellationToken token = default)
		{
			// https://tools.ietf.org/html/rfc7230#section-2.7.1
			// A sender MUST NOT generate an "http" URI with an empty host identifier.
			string host = GetRequestHost(request);
			int port = request.RequestUri!.Port;

			// https://tools.ietf.org/html/rfc7230#section-2.6
			// Intermediaries that process HTTP messages (i.e., all intermediaries
			// other than those acting as tunnels) MUST send their own HTTP - version
			// in forwarded messages.
			request.Version = HttpProtocol.HTTP11.Version;

			return TorSocks5ClientFactory.MakeAsync(isolateStream, host, port, useSsl, token);
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
					ClientsManager.Dispose();
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