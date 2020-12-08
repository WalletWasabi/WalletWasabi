using System;
using System.Net;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5;

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

			TorSocks5ClientPool = torEndPoint is null ? null : TorSocks5ClientPool.Create(torEndPoint);
			SharedWasabiClient = new WasabiClient(NewBackendTorHttpClient(false));
		}

		/// <summary>Tor SOCKS5 endpoint.</summary>
		/// <remarks>The property should be <c>>private</c> when Tor refactoring is done.</remarks>
		public EndPoint? TorEndpoint { get; }

		/// <remarks>The property should be <c>>private</c> when Tor refactoring is done.</remarks>
		public Func<Uri> BackendUriGetter { get; }
		public TorSocks5ClientPool? TorSocks5ClientPool { get; }

		/// <summary>Whether Tor is enabled or disabled.</summary>
		public bool IsTorEnabled => TorEndpoint is { };

		/// <summary>Shared instance of <see cref="WasabiClient"/>.</summary>
		public WasabiClient SharedWasabiClient { get; }

		/// <summary>
		/// Creates new <see cref="TorHttpClient"/> or <see cref="ClearnetHttpClient"/> based on user settings.
		/// </summary>
		public IRelativeHttpClient NewHttpClient(Func<Uri> baseUriFn, bool isolateStream)
		{
			if (TorSocks5ClientPool is { })
			{
				return new TorHttpClient(TorSocks5ClientPool, baseUriFn, isolateStream);
			}
			else
			{
				return new ClearnetHttpClient(baseUriFn);
			}
		}

		/// <summary>
		/// Creates new <see cref="TorHttpClient"/> instance with correctly set backend URL.
		/// </summary>
		public IRelativeHttpClient NewBackendTorHttpClient(bool isolateStream)
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
				TorSocks5ClientPool?.Dispose();
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