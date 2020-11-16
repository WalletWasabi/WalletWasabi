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
		public TorSocks5ClientPool(EndPoint endpoint, bool isolateStream)
		{
			IsolateStream = isolateStream;

			TorSocks5ClientFactory = new TorSocks5ClientFactory(endpoint);
			Clients = new Dictionary<string, List<PoolItem>>();
		}

		private bool IsolateStream { get; }
		private TorSocks5ClientFactory TorSocks5ClientFactory { get; }
		private bool _disposedValue;

		/// <remarks>Lock object to guard all access to <see cref="Clients"/>.</remarks>
		private AsyncLock ClientsAsyncLock { get; } = new AsyncLock();

		/// <summary>Key is always a URI host. Value is a list of pool items that can connect to the URI host.</summary>
		/// <remarks>All access to this object must be guarded by <see cref="ClientsAsyncLock"/>.</remarks>
		private Dictionary<string, List<PoolItem>> Clients { get; }

		/// <summary>
		/// Robust sending algorithm. TODO.
		/// </summary>
		/// <param name="request"></param>
		/// <param name="token"></param>
		public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
		{
			int i = 0;
			int attemptsNo = 3;

			do
			{
				i++;
				PoolItem? poolItem;
				TorConnection? client = null;

				do
				{
					using (await ClientsAsyncLock.LockAsync(token).ConfigureAwait(false))
					{
						poolItem = await GetClientLockedAsync(request, token).ConfigureAwait(false);

						if (poolItem is { })
						{
							client = poolItem.GetClient();
							break;
						}
					}

					Logger.LogTrace("Wait 1s for a free pool item.");
					await Task.Delay(1000, token).ConfigureAwait(false);
				} while (poolItem is null);

				PoolItem? itemToDispose = poolItem;

				try
				{
					Logger.LogTrace($"['{poolItem}'] About to send request.");
					HttpResponseMessage response = await SendCoreAsync(client!, request).ConfigureAwait(false);

					// Client works OK, no need to dispose.
					itemToDispose = null;

					// Let others use the client.
					var state = poolItem.Unreserve();
					Logger.LogTrace($"['{poolItem}'] Un-reserve. State is: '{state}'.");

					return response;
				}
				catch (TorHttpResponseException ex) when (ex.RepField == RepField.TtlExpired)
				{
					// If we get TTL Expired error then wait and retry again linux often do this.
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
					itemToDispose?.Dispose();
				}
			} while (i < attemptsNo);

			throw new NotImplementedException("This should never happen.");
		}

		/// <remarks>Caller is responsible for acquiring <see cref="ClientsAsyncLock"/>.</remarks>
		private async Task<PoolItem?> GetClientLockedAsync(HttpRequestMessage request, CancellationToken token)
		{
			string host = GetRequestHost(request);

			PoolItem? reservedItem;

			// Make sure the list is present.
			if (!Clients.ContainsKey(host))
			{
				Clients.Add(host, new List<PoolItem>());
			}

			// Get list of connections for given host.
			List<PoolItem> hostItems = Clients[host];

			// Find first free connection, if it exists.
			List<PoolItem> disposeList = hostItems.FindAll(item => item.NeedRecycling()).ToList();

			// Remove items for disposal from the list.
			disposeList.ForEach(item => hostItems.Remove(item));

			// Find first free connection, if it exists.
			reservedItem = hostItems.Find(item => item.TryReserve());

			Logger.LogTrace($"Get PoolItem for '{host}' host; Context: hostItems = {string.Join(',', hostItems.Select(x => x.ToString()).ToArray())}.");

			if (reservedItem is null)
			{
				if (hostItems.Count > MaxPoolItemsPerHost)
				{
					Logger.LogTrace($"['{host}'][NONE] No free pool item.");
				}
				else
				{
					try
					{
						bool useSsl = request.RequestUri.Scheme == "https";

						TorConnection newClient = await NewSocks5ClientAsync(request, useSsl, token).ConfigureAwait(false);
						reservedItem = new PoolItem(newClient, allowRecycling: !useSsl);

						Logger.LogTrace($"[NEW {reservedItem}]['{request.RequestUri}'] Created new Tor SOCKS5 connection.");

						hostItems.Add(reservedItem);
					}
					catch (TorException e)
					{
						Logger.LogDebug($"['{host}'][ERROR] Failed to create a new pool item.");
						Logger.LogError(e);
					}
				}
			}
			else
			{
				Logger.LogTrace($"[OLD {reservedItem}]['{request.RequestUri}'] Re-use existing Tor SOCKS5 connection.");
			}

			return reservedItem;
		}

		private async Task<HttpResponseMessage> SendCoreAsync(TorConnection client, HttpRequestMessage request, CancellationToken token = default)
		{
			request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

			// https://tools.ietf.org/html/rfc7230#section-3.3.2
			// A user agent SHOULD send a Content - Length in a request message when
			// no Transfer-Encoding is sent and the request method defines a meaning
			// for an enclosed payload body.For example, a Content - Length header
			// field is normally sent in a POST request even when the value is 0
			// (indicating an empty payload body).A user agent SHOULD NOT send a
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
						request.Content.Headers.ContentLength ??= (await request.Content.ReadAsStringAsync().ConfigureAwait(false)).Length;
					}
				}
			}

			string requestString = await request.ToHttpStringAsync().ConfigureAwait(false);

			var bytes = Encoding.UTF8.GetBytes(requestString);

			Stream transportStream = client.GetTransportStream();

			await transportStream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
			await transportStream.FlushAsync(token).ConfigureAwait(false);

			return await HttpResponseMessageExtensions.CreateNewAsync(transportStream, request.Method).ConfigureAwait(false);
		}

		/// <inheritdoc cref="TorSocks5ClientFactory.MakeAsync(bool, string, int, bool, CancellationToken)"/>
		public Task<TorConnection> NewSocks5ClientAsync(HttpRequestMessage request, bool useSsl, CancellationToken token = default)
		{
			// https://tools.ietf.org/html/rfc7230#section-2.7.1
			// A sender MUST NOT generate an "http" URI with an empty host identifier.
			string host = GetRequestHost(request);
			int port = request.RequestUri.Port;

			// https://tools.ietf.org/html/rfc7230#section-2.6
			// Intermediaries that process HTTP messages (i.e., all intermediaries
			// other than those acting as tunnels) MUST send their own HTTP - version
			// in forwarded messages.
			request.Version = HttpProtocol.HTTP11.Version;

			return TorSocks5ClientFactory.MakeAsync(IsolateStream, host, port, useSsl, token);
		}

		private static string GetRequestHost(HttpRequestMessage request)
		{
			return Guard.NotNullOrEmptyOrWhitespace(nameof(request.RequestUri.DnsSafeHost), request.RequestUri.DnsSafeHost, trim: true);
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
					foreach (List<PoolItem> list in Clients.Values)
					{
						foreach (PoolItem item in list)
						{
							// TODO: Disposing: identifier?
							item.Dispose();
						}
					}
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