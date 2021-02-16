using System;
using System.Net;
using System.Net.Http;
using WalletWasabi.Tor.Http;

namespace WalletWasabi.WebClients.Wasabi
{
	/// <summary>
	/// Factory class to get proper <see cref="IHttpClient"/> client which is set up based on user settings.
	/// </summary>
	public class HttpClientFactory
	{
		/// <summary>
		/// To detect redundant calls.
		/// </summary>
		private bool _disposed = false;

		/// <summary>
		/// Creates a new instance of the object.
		/// </summary>
		/// <param name="torEndPoint">If <c>null</c> then clearnet (not over Tor) is used, otherwise HTTP requests are routed through provided Tor endpoint.</param>
		public HttpClientFactory(EndPoint? torEndPoint, Func<Uri> backendUriGetter)
		{
			SocketHandler = new SocketsHttpHandler()
			{
				// Only GZip is currently used by Wasabi Backend.
				AutomaticDecompression = DecompressionMethods.GZip,
				PooledConnectionLifetime = TimeSpan.FromMinutes(5)
			};

			HttpClient = new HttpClient(SocketHandler);

			TorEndpoint = torEndPoint;
			BackendUriGetter = backendUriGetter;

			// Connecting to loopback's URIs cannot be done via Tor.
			if (TorEndpoint is { } && !BackendUriGetter().IsLoopback)
			{
				BackendHttpClient = new TorHttpClient(BackendUriGetter, TorEndpoint, isolateStream: false);
			}
			else
			{
				BackendHttpClient = new ClearnetHttpClient(HttpClient, BackendUriGetter);
			}

			SharedWasabiClient = new WasabiClient(BackendHttpClient);
		}

		/// <summary>Tor SOCKS5 endpoint.</summary>
		/// <remarks>The property should be <c>>private</c> when Tor refactoring is done.</remarks>
		public EndPoint? TorEndpoint { get; }

		/// <remarks>The property should be <c>>private</c> when Tor refactoring is done.</remarks>
		public Func<Uri> BackendUriGetter { get; }

		/// <summary>Whether Tor is enabled or disabled.</summary>
		public bool IsTorEnabled => TorEndpoint is { };

		private SocketsHttpHandler SocketHandler { get; }

		/// <summary>.NET HTTP client to be used by <see cref="ClearnetHttpClient"/> instances.</summary>
		private HttpClient HttpClient { get; }

		/// <summary>Backend HTTP client, shared instance.</summary>
		private IHttpClient BackendHttpClient { get; }

		/// <summary>Shared instance of <see cref="WasabiClient"/>.</summary>
		public WasabiClient SharedWasabiClient { get; }

		/// <summary>
		/// Creates new <see cref="TorHttpClient"/> or <see cref="ClearnetHttpClient"/> based on user settings.
		/// </summary>
		public IHttpClient NewHttpClient(Func<Uri> baseUriFn, bool isolateStream)
		{
			// Connecting to loopback's URIs cannot be done via Tor.
			if (TorEndpoint is { } && !BackendUriGetter().IsLoopback)
			{
				return new TorHttpClient(baseUriFn, TorEndpoint, isolateStream);
			}
			else
			{
				return new ClearnetHttpClient(HttpClient, baseUriFn);
			}
		}

		/// <summary>
		/// Creates a new <see cref="IHttpClient"/> with the base URI is set to Wasabi Backend.
		/// </summary>
		public IHttpClient NewBackendHttpClient(bool isolateStream)
		{
			return NewHttpClient(BackendUriGetter, isolateStream);
		}

		// Protected implementation of Dispose pattern.
		protected virtual void Dispose(bool disposing)
		{
			if (_disposed)
			{
				return;
			}

			if (disposing)
			{
				// Dispose managed state (managed objects).
				if (BackendHttpClient is IDisposable httpClient)
				{
					httpClient.Dispose();
				}

				HttpClient.Dispose();
				SocketHandler.Dispose();
			}

			_disposed = true;
		}

		public void Dispose()
		{
			// Dispose of unmanaged resources.
			Dispose(true);

			// Suppress finalization.
			GC.SuppressFinalize(this);
		}
	}
}
