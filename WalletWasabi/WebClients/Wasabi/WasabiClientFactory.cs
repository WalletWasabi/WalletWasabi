using System;
using System.Net;
using WalletWasabi.Tor.Http;

namespace WalletWasabi.WebClients.Wasabi
{
	/// <summary>
	/// Factory class to create <see cref="WasabiClient"/> instances.
	/// </summary>
	public class WasabiClientFactory
	{
		/// <summary>
		/// Creates a new instance of the object.
		/// </summary>
		/// <param name="torEndPoint">If <c>null</c> then clearnet (not over Tor) is used, otherwise HTTP requests are routed through provided Tor endpoint.</param>
		public WasabiClientFactory(EndPoint? torEndPoint, Func<Uri> backendUriGetter)
		{
			TorEndpoint = torEndPoint;
			BackendUriGetter = backendUriGetter;
		}

		/// <summary>Tor SOCKS5 endpoint.</summary>
		/// <remarks>The property should be <c>>private</c> when Tor refactoring is done.</remarks>
		public EndPoint? TorEndpoint { get; }

		/// <remarks>The property should be <c>>private</c> when Tor refactoring is done.</remarks>
		public Func<Uri> BackendUriGetter { get; }

		/// <summary>Whether Tor is enabled or disabled.</summary>
		public bool IsTorEnabled => TorEndpoint is { };

		/// <summary>
		/// Creates new <see cref="WasabiClient"/> instance with correctly set backend URL.
		/// </summary>
		/// <remarks>
		/// For privacy reasons, it is advisable to create a new instance of <see cref="WasabiClient"/>
		/// for each HTTP request instead of reusing one instance for multiple HTTP requests.
		/// </remarks>
		public WasabiClient NewBackendClient(bool isolateStream = false)
		{
			TorHttpClient torHttpClient = new TorHttpClient(BackendUriGetter.Invoke(), TorEndpoint, isolateStream: isolateStream);
			return new WasabiClient(torHttpClient);
		}

		/// <summary>
		/// Creates new <see cref="TorHttpClient"/>.
		/// </summary>
		public TorHttpClient NewBackendTorHttpClient(bool isolateStream)
		{
			return new TorHttpClient(BackendUriGetter, TorEndpoint, isolateStream);
		}
	}
}
