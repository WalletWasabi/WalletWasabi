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
		/// Creates a new instance of <see cref="WasabiClient"/> which is predefined with <paramref name="baseUri"/> parameter.
		/// </summary>
		/// <remarks>New instances are better to increase privacy level.</remarks>
		/// <param name="baseUri">Base URI used in all HTTP requests created by <see cref="WasabiClient"/> class.</param>
		/// <param name="isolateStream">Whether new Tor identity should be used for all HTTP requests.</param>
		public WasabiClient Create(Uri baseUri, bool isolateStream = true)
		{
			TorHttpClient torHttpClient = new TorHttpClient(baseUri, TorEndpoint, isolateStream);
			return new WasabiClient(torHttpClient);
		}

		/// <summary>
		/// Creates new <see cref="WasabiClient"/> instance with correctly set backend URL.
		/// </summary>
		/// <remarks>
		/// For privacy reasons, it is advisable to create a new instance of <see cref="WasabiClient"/>
		/// for each HTTP request instead of reusing one instance for multiple HTTP requests.
		/// </remarks>
		/// <param name="isolateStream">Whether a new Tor identity should be used for this <see cref="WasabiClient"/> instance.</param>
		public WasabiClient NewBackendClient(bool isolateStream = true)
		{
			TorHttpClient torHttpClient = new TorHttpClient(BackendUriGetter.Invoke(), TorEndpoint, isolateStream);
			return new WasabiClient(torHttpClient);
		}
	}
}
