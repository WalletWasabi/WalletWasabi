using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Http.Interfaces;
using WalletWasabi.Tor.Socks5;

namespace WalletWasabi.Tor.Http
{
	public class TorHttpClient : IRelativeHttpClient, IDisposable
	{
		private static DateTimeOffset? TorDoesntWorkSinceBacking = null;

		/// <summary>
		/// To detect redundant calls.
		/// </summary>
		private volatile bool _disposed = false;

		public TorHttpClient(Uri baseUri, EndPoint? torSocks5EndPoint, bool isolateStream = false) :
			this(() => baseUri, torSocks5EndPoint, isolateStream)
		{
			baseUri = Guard.NotNull(nameof(baseUri), baseUri);
		}

		public TorHttpClient(Func<Uri> baseUriAction, EndPoint? torSocks5EndPoint, bool isolateStream = false)
		{
			DestinationUriAction = Guard.NotNull(nameof(baseUriAction), baseUriAction);

			// Connecting to loopback's URIs cannot be done via Tor.
			TorSocks5EndPoint = DestinationUriAction().IsLoopback ? null : torSocks5EndPoint;

			ForceIsolateStream = isolateStream;

			// Pool can be only one.
			lock (PoolLock)
			{
				InstanceCounter++;

				if (TorSocks5EndPoint is { } && TorSocks5ClientPool is null)
				{
					TorSocks5ClientPool = new TorSocks5ClientPool(TorSocks5EndPoint, isolateStream);
				}
			}			
		}

		public static Exception? LatestTorException { get; private set; } = null;
		public Func<Uri> DestinationUriAction { get; }
		private EndPoint? TorSocks5EndPoint { get; }
		public bool IsTorUsed => TorSocks5EndPoint is { };

		/// <summary>Lock object to protect access to <see cref="TorSocks5ClientPool"/>.</summary>
		private static object PoolLock { get; } = new object();

		/// <remarks>All access to this object must be guarded by <see cref="PoolLock"/>.</remarks>
		private static TorSocks5ClientPool? TorSocks5ClientPool { get; set; }

		/// <summary>All HTTP(s) requests sent by this HTTP client must use different Tor circuits.</summary>
		private bool ForceIsolateStream { get; }		

		/// <remarks>All access to this object must be guarded by <see cref="PoolLock"/>.</remarks>
		private static int InstanceCounter { get; set; }

		public static DateTimeOffset? TorDoesntWorkSince
		{
			get => TorDoesntWorkSinceBacking;
			private set
			{
				if (value != TorDoesntWorkSinceBacking)
				{
					TorDoesntWorkSinceBacking = value;
					if (value is null)
					{
						LatestTorException = null;
					}
				}
			}
		}

		private Task<HttpResponseMessage> ClearnetRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
		{
			return new ClearnetHttpClient(DestinationUriAction).SendAsync(request, cancellationToken);
		}

		/// <remarks>
		/// Throws <see cref="OperationCanceledException"/> if <paramref name="token"/> is set.
		/// </remarks>
		public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken token = default)
		{
			var requestUri = new Uri(DestinationUriAction(), relativeUri);
			using var request = new HttpRequestMessage(method, requestUri);

			if (content is { })
			{
				request.Content = content;
			}

			return await SendAsync(request, token).ConfigureAwait(false);
		}

		private static void SetTorNotWorkingState(Exception ex)
		{
			TorDoesntWorkSince ??= DateTimeOffset.UtcNow;
			LatestTorException = ex;
		}

		/// <exception cref="OperationCanceledException">If <paramref name="token"/> is set.</exception>
		public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
		{
			// Use clearnet HTTP client when Tor is disabled.
			if (TorSocks5EndPoint is null)
			{
				return await ClearnetRequestAsync(request, token).ConfigureAwait(false);
			}
			else
			{
				try
				{
					HttpResponseMessage httpResponseMessage = await TorSocks5ClientPool!.SendAsync(request, token).ConfigureAwait(false);
					TorDoesntWorkSince = null;

					return httpResponseMessage;
				}
				catch (OperationCanceledException ex)
				{
					SetTorNotWorkingState(ex);
					throw;
				}
				catch (Exception ex)
				{
					SetTorNotWorkingState(ex);
					throw;
				}
			}
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
			if (!_disposed)
			{
				if (disposing)
				{
					if (TorSocks5EndPoint is { })
					{
						lock (PoolLock)
						{
							InstanceCounter--;

							if (InstanceCounter <= 0)
							{
								TorSocks5ClientPool?.Dispose();
								TorSocks5ClientPool = null;
							}
						}
					}
				}
				_disposed = true;
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
