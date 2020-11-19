using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor
{
	/// <summary>
	/// Monitors Tor process.
	/// </summary>
	public class TorMonitor : PeriodicRunner
	{
		public static readonly TimeSpan CheckIfRunningAfterTorMisbehavedFor = TimeSpan.FromSeconds(7);

		/// <summary>
		/// Creates a new instance of the object.
		/// </summary>
		public TorMonitor(TimeSpan period, Uri fallBackTestRequestUri, EndPoint torSocks5EndPoint, TorProcessManager torProcessManager) : base(period)
		{
			FallBackTestRequestUri = fallBackTestRequestUri;
			TorSocks5EndPoint = torSocks5EndPoint;
			TorProcessManager = torProcessManager;
		}

		public static bool RequestFallbackAddressUsage { get; private set; } = false;
		private Uri FallBackTestRequestUri { get; }
		private EndPoint TorSocks5EndPoint { get; }
		private TorProcessManager TorProcessManager { get; }

		/// <inheritdoc/>
		protected override async Task ActionAsync(CancellationToken token)
		{
			if (TorHttpClient.TorDoesntWorkSince is { }) // If Tor misbehaves.
			{
				TimeSpan torMisbehavedFor = DateTimeOffset.UtcNow - TorHttpClient.TorDoesntWorkSince ?? TimeSpan.Zero;

				if (torMisbehavedFor > CheckIfRunningAfterTorMisbehavedFor)
				{
					if (TorHttpClient.LatestTorException is TorSocks5FailureResponseException torEx)
					{
						if (torEx.RepField == RepField.HostUnreachable)
						{
							Uri baseUri = new Uri($"{FallBackTestRequestUri.Scheme}://{FallBackTestRequestUri.DnsSafeHost}");
							using (var client = new TorHttpClient(baseUri, TorSocks5EndPoint))
							{
								var message = new HttpRequestMessage(HttpMethod.Get, FallBackTestRequestUri);
								await client.SendAsync(message, token).ConfigureAwait(false);
							}

							// Check if it changed in the meantime...
							if (TorHttpClient.LatestTorException is TorSocks5FailureResponseException torEx2 && torEx2.RepField == RepField.HostUnreachable)
							{
								// Fallback here...
								RequestFallbackAddressUsage = true;
							}
						}
					}
					else
					{
						Logger.LogInfo($"Tor did not work properly for {(int)torMisbehavedFor.TotalSeconds} seconds. Maybe it crashed. Attempting to start it...");

						// Try starting Tor, if it does not work it'll be another issue.
						bool started = await TorProcessManager.StartAsync(ensureRunning: true).ConfigureAwait(false);

						Logger.LogInfo($"Tor re-starting attempt {(started ? "succeeded." : "FAILED. Will try again later.")}");
					}
				}
			}
		}
	}
}
