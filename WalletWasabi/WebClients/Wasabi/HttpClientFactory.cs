using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool;

namespace WalletWasabi.WebClients.Wasabi
{
	/// <summary>
	/// Factory class to get proper <see cref="IHttpClient"/> client which is set up based on user settings.
	/// </summary>
	public class HttpClientFactory : IDisposable
	{
		/// <summary>
		/// To detect redundant calls.
		/// </summary>
		private bool _disposed = false;

		/// <summary>
		/// Creates a new instance of the object.
		/// </summary>
		/// <param name="torEndPoint">If <c>null</c> then clearnet (not over Tor) is used, otherwise HTTP requests are routed through provided Tor endpoint.</param>
		public HttpClientFactory(EndPoint? torEndPoint, Func<Uri>? backendUriGetter)
		{
			SocketHandler = new()
			{
				// Only GZip is currently used by Wasabi Backend.
				AutomaticDecompression = DecompressionMethods.GZip,
				PooledConnectionLifetime = TimeSpan.FromMinutes(5)
			};

			HttpClient = new(SocketHandler);

			TorEndpoint = torEndPoint;
			BackendUriGetter = backendUriGetter;

			// Connecting to loopback's URIs cannot be done via Tor.
			if (TorEndpoint is { } && (BackendUriGetter is null || !BackendUriGetter().IsLoopback))
			{
				TorHttpPool = new(TorEndpoint);
				BackendHttpClient = new TorHttpClient(BackendUriGetter, TorHttpPool, isolateStream: false);
			}
			else
			{
				BackendHttpClient = new ClearnetHttpClient(HttpClient, BackendUriGetter);
			}

			SharedWasabiClient = new(BackendHttpClient);
		}

		/// <summary>Tor SOCKS5 endpoint.</summary>
		/// <remarks>The property should be <c>private</c> when Tor refactoring is done.</remarks>
		public EndPoint? TorEndpoint { get; }

		/// <remarks>The property should be <c>private</c> when Tor refactoring is done.</remarks>
		public Func<Uri>? BackendUriGetter { get; }

		/// <summary>Whether Tor is enabled or disabled.</summary>
		[MemberNotNullWhen(returnValue: true, nameof(TorEndpoint))]
		public bool IsTorEnabled => TorEndpoint is { };

		private SocketsHttpHandler SocketHandler { get; }

		/// <summary>.NET HTTP client to be used by <see cref="ClearnetHttpClient"/> instances.</summary>
		private HttpClient HttpClient { get; }

		/// <summary>Available only when Tor is enabled in User settings.</summary>
		private TorHttpPool? TorHttpPool { get; }

		/// <summary>Backend HTTP client, shared instance.</summary>
		private IHttpClient BackendHttpClient { get; }

		/// <summary>Shared instance of <see cref="WasabiClient"/>.</summary>
		public WasabiClient SharedWasabiClient { get; }

		/// <summary>
		/// Creates new <see cref="TorHttpClient"/> or <see cref="ClearnetHttpClient"/> based on user settings.
		/// </summary>
		public IHttpClient NewHttpClient(Func<Uri>? baseUriFn, bool isolateStream)
		{
			// Connecting to loopback's URIs cannot be done via Tor.
			if (TorHttpPool is { } && (BackendUriGetter is null || !BackendUriGetter().IsLoopback))
			{
				return new TorHttpClient(baseUriFn, TorHttpPool, isolateStream);
			}
			else
			{
				return new ClearnetHttpClient(HttpClient, baseUriFn);
			}
		}

		/// <summary>Creates new <see cref="TorHttpClient"/>.</summary>
		/// <remarks>Do not use this function unless <see cref="NewHttpClient(Func{Uri}, bool)"/> is not sufficient for your use case.</remarks>
		/// <exception cref="InvalidOperationException"/>
		public TorHttpClient NewTorHttpClient(bool isolateStream, Func<Uri>? baseUriFn = null)
		{
			if (TorEndpoint is null)
			{
				throw new InvalidOperationException("Tor is not enabled in the user settings.");
			}

			return (TorHttpClient)NewHttpClient(baseUriFn, isolateStream);
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
				TorHttpPool?.Dispose();
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
