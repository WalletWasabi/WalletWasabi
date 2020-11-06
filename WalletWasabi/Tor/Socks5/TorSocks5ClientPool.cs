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
using WalletWasabi.Tor.Exceptions;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.Tor.Http.Models;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;
using WalletWasabi.Tor.Socks5.Pool;

namespace WalletWasabi.Tor.Socks5
{
	/// <summary>
	/// TODO.
	/// </summary>
	public class TorSocks5ClientPool : IDisposable
	{
		/// <summary>
		/// Creates a new instance of the object.
		/// </summary>
		public TorSocks5ClientPool(EndPoint endpoint, bool isolateStream)
		{
			Endpoint = endpoint;
			IsolateStream = isolateStream;

			Clients = new Dictionary<string, List<PoolItem>>();
		}

		/// <summary>Tor SOCKS5 endpoint.</summary>
		private EndPoint Endpoint { get; }
		private bool IsolateStream { get; }

		private bool _disposedValue;

		private AsyncLock ClientsAsyncLock { get; } = new AsyncLock();

		/// <summary>TODO.</summary>
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
				TorSocks5Client? client = null;

				do
				{
					using (await ClientsAsyncLock.LockAsync(token).ConfigureAwait(false))
					{
						poolItem = await GetClientAsync(request, token).ConfigureAwait(false);

						if (poolItem is { })
						{
							client = poolItem.GetClient();
							break;
						}
					}

					Logger.LogTrace("Wait 1s for a free pool item.");
					await Task.Delay(1000, token).ConfigureAwait(false);
				} while (poolItem is null);

				PoolItem ? itemToDispose = poolItem;

				try
				{
					Logger.LogDebug($"Do the request using '{poolItem}'.");
					HttpResponseMessage response = await SendCoreAsync(client!, request).ConfigureAwait(false);

					// Client works OK, no need to dispose.
					itemToDispose = null;

					// Let others use the client.
					poolItem.Unreserve();

					return response;
				}
				catch (TorSocks5FailureResponseException ex) when (ex.RepField == RepField.TtlExpired)
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
				catch (SocketException ex3) when (ex3.ErrorCode == (int)SocketError.ConnectionRefused)
				{
					throw new ConnectionException("Connection was refused.", ex3);
				}
				finally
				{
					itemToDispose?.Dispose();
				}
			} while (i < attemptsNo);

			throw new NotImplementedException("This should never happen.");
		}

		public async Task<PoolItem?> GetClientAsync(HttpRequestMessage request, CancellationToken token)
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

			if (reservedItem is null)
			{
				if (hostItems.Count > 3)
				{
					Logger.LogTrace($"[NONE] No free pool item.");
				}
				else
				{
					// TODO: Handle exceptions.
					TorSocks5Client newClient = await NewClientAsync(request, token).ConfigureAwait(false);
					reservedItem = new PoolItem(newClient);

					Logger.LogTrace($"[NEW {reservedItem}]['{request.RequestUri}'] Created new Tor SOCKS5 connection.");

					hostItems.Add(reservedItem);
				}
			}
			else
			{
				Logger.LogTrace($"[OLD {reservedItem}]['{request.RequestUri}'] Re-use existing Tor SOCKS5 connection.");
			}

			return reservedItem;
		}

		private async Task<HttpResponseMessage> SendCoreAsync(TorSocks5Client client, HttpRequestMessage request, CancellationToken token = default)
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

		public async Task<TorSocks5Client> NewClientAsync(HttpRequestMessage request, CancellationToken token = default)
		{
			// https://tools.ietf.org/html/rfc7230#section-2.7.1
			// A sender MUST NOT generate an "http" URI with an empty host identifier.
			string host = GetRequestHost(request);

			// https://tools.ietf.org/html/rfc7230#section-2.6
			// Intermediaries that process HTTP messages (i.e., all intermediaries
			// other than those acting as tunnels) MUST send their own HTTP - version
			// in forwarded messages.
			request.Version = HttpProtocol.HTTP11.Version;

			var client = new TorSocks5Client(Endpoint);
			await client.ConnectAsync().ConfigureAwait(false);
			await client.HandshakeAsync(IsolateStream, token).ConfigureAwait(false);
			await client.ConnectToDestinationAsync(host, request.RequestUri.Port, token).ConfigureAwait(false);

			if (request.RequestUri.Scheme == "https")
			{
				await client.UpgradeToSslAsync(host).ConfigureAwait(false);
			}

			return client;
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
