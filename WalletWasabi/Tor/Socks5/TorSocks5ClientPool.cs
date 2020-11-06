using System;
using System.IO;
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

namespace WalletWasabi.Tor.Socks5
{
	/// <summary>
	/// TODO.
	/// </summary>
	public class TorSocks5ClientPool: IDisposable
	{
		/// <summary>
		/// Creates a new instance of the object.
		/// </summary>
		public TorSocks5ClientPool(EndPoint endpoint, bool isolateStream)
		{
			Endpoint = endpoint;
			IsolateStream = isolateStream;
		}

		/// <summary>Tor SOCKS5 endpoint.</summary>
		private EndPoint Endpoint { get; }
		private bool IsolateStream { get; }

		private bool _disposedValue;

		/// <summary>TODO.</summary>
		private TorSocks5Client? TorSocks5Client { get; set; }

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

				TorSocks5Client client = await GetOrCreateNewClientAsync(request, token).ConfigureAwait(false);
				TorSocks5Client? clientToDispose = client;

				try
				{
					HttpResponseMessage response = await SendCoreAsync(client, request).ConfigureAwait(false);
					return response;
				}
				catch (TorSocks5FailureResponseException ex) when (ex.RepField == RepField.TtlExpired)
				{
					// If we get TTL Expired error then wait and retry again linux often do this.
					Logger.LogTrace(ex);

					TorSocks5Client?.Dispose(); // rebuild the connection and retry
					TorSocks5Client = null;

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
					clientToDispose?.Dispose();
				}
			} while (i < attemptsNo);

			throw new NotImplementedException("This should never happen.");
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

		public async Task<TorSocks5Client> GetOrCreateNewClientAsync(HttpRequestMessage request, CancellationToken token)
		{
			if (TorSocks5Client is { } && !TorSocks5Client.IsConnected)
			{
				TorSocks5Client?.Dispose();
				TorSocks5Client = null;
			}

			if (TorSocks5Client is null || !TorSocks5Client.IsConnected)
			{
				TorSocks5Client = await NewClientAsync(request, token).ConfigureAwait(false);
			}

			return TorSocks5Client;
		}

		public async Task<TorSocks5Client> NewClientAsync(HttpRequestMessage request, CancellationToken token)
		{
			// https://tools.ietf.org/html/rfc7230#section-2.7.1
			// A sender MUST NOT generate an "http" URI with an empty host identifier.
			string host = Guard.NotNullOrEmptyOrWhitespace(nameof(request.RequestUri.DnsSafeHost), request.RequestUri.DnsSafeHost, trim: true);

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
					TorSocks5Client?.Dispose();
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
