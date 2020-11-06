using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Http.Models;

namespace WalletWasabi.Tor.Socks5
{
	/// <summary>
	/// TODO.
	/// </summary>
	public class TorSocks5ClientPool
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
	}
}
