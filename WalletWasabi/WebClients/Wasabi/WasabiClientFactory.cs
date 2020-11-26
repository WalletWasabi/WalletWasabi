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
		/// To detect redundant calls.
		/// </summary>
		private bool _disposed = false;

		/// <summary>
		/// Creates a new instance of the object.
		/// </summary>
		/// <param name="torEndPoint">If <c>null</c> then clearnet (not over Tor) is used, otherwise HTTP requests are routed through provided Tor endpoint.</param>
		public WasabiClientFactory(EndPoint? torEndPoint, Func<Uri> backendUriGetter)
		{
			TorEndpoint = torEndPoint;
			BackendUriGetter = backendUriGetter;

			SharedTorHttpClient = new TorHttpClient(BackendUriGetter.Invoke(), TorEndpoint, isolateStream: false);
			SharedWasabiClient = new WasabiClient(SharedTorHttpClient);
		}

		/// <summary>Tor SOCKS5 endpoint.</summary>
		/// <remarks>The property should be <c>>private</c> when Tor refactoring is done.</remarks>
		public EndPoint? TorEndpoint { get; }

		/// <remarks>The property should be <c>>private</c> when Tor refactoring is done.</remarks>
		public Func<Uri> BackendUriGetter { get; }

		/// <summary>Whether Tor is enabled or disabled.</summary>
		public bool IsTorEnabled => TorEndpoint is { };

		/// <summary>Shared instance of <see cref="TorHttpClient"/>.</summary>
		public TorHttpClient SharedTorHttpClient { get; }

		/// <summary>Shared instance of <see cref="WasabiClient"/>.</summary>
		public WasabiClient SharedWasabiClient { get; }

		/// <summary>
		/// Creates new <see cref="TorHttpClient"/> instance with correctly set backend URL.
		/// </summary>
		public TorHttpClient NewTorHttpClient(bool isolateStream = true)
		{
			return new TorHttpClient(BackendUriGetter.Invoke(), TorEndpoint, isolateStream: isolateStream);
		}

		/// <summary>
		/// Creates new <see cref="TorHttpClient"/> or <see cref="ClearnetHttpClient"/> based on user settings.
		/// </summary>
		public IRelativeHttpClient NewHttpClient(Func<Uri> baseUriFn, bool isolateStream)
		{
			if (TorEndpoint is { })
			{
				return new TorHttpClient(baseUriFn, TorEndpoint, isolateStream);
			}
			else
			{
				return new ClearnetHttpClient(baseUriFn);
			}
		}

		/// <summary>
		/// Creates new <see cref="TorHttpClient"/> instance with correctly set backend URL.
		/// </summary>
		public TorHttpClient NewBackendTorHttpClient(bool isolateStream)
		{
			return new TorHttpClient(BackendUriGetter, TorEndpoint, isolateStream);
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
				SharedTorHttpClient.Dispose();
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