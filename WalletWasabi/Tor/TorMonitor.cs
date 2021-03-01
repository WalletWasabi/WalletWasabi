using System;
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
		public TorMonitor(TimeSpan period, Uri fallbackBackendUri, TorHttpClient httpClient, TorProcessManager torProcessManager) : base(period)
		{
			FallbackBackendUri = fallbackBackendUri;
			HttpClient = httpClient;
			TorProcessManager = torProcessManager;
		}

		public static bool RequestFallbackAddressUsage { get; private set; } = false;
		private Uri FallbackBackendUri { get; }
		private TorHttpClient HttpClient { get; }
		private TorProcessManager TorProcessManager { get; }

		/// <inheritdoc/>
		protected override async Task ActionAsync(CancellationToken token)
		{
			if (TorHttpClient.TorDoesntWorkSince is { }) // If Tor misbehaves.
			{
				TimeSpan torMisbehavedFor = DateTimeOffset.UtcNow - TorHttpClient.TorDoesntWorkSince ?? TimeSpan.Zero;

				if (torMisbehavedFor > CheckIfRunningAfterTorMisbehavedFor)
				{
					if (TorHttpClient.LatestTorException is TorConnectCommandFailedException torEx)
					{
						if (torEx.RepField == RepField.HostUnreachable)
						{
							Logger.LogInfo("Tor does not work properly. Test fallback URI.");
							using HttpRequestMessage request = new(HttpMethod.Get, FallbackBackendUri);
							using var _ = await HttpClient.SendAsync(request, token).ConfigureAwait(false);

							// Check if it changed in the meantime...
							if (TorHttpClient.LatestTorException is TorConnectCommandFailedException torEx2 && torEx2.RepField == RepField.HostUnreachable)
							{
								// Fallback here...
								RequestFallbackAddressUsage = true;
							}
						}
					}
					else
					{
						bool isRunning = await TorProcessManager.IsTorRunningAsync().ConfigureAwait(false);

						if (!isRunning)
						{
							Logger.LogInfo($"Tor did not work properly for {(int)torMisbehavedFor.TotalSeconds} seconds. Maybe it crashed. Attempting to start it...");

							// Try starting Tor, if it does not work it'll be another issue.
							bool started = await TorProcessManager.StartAsync(ensureRunning: true).ConfigureAwait(false);

							Logger.LogInfo($"Tor re-starting attempt {(started ? "succeeded." : "FAILED. Will try again later.")}");
						}
						else
						{
							Logger.LogInfo("Tor is running. Waiting for a confirmation that HTTP requests can pass through.");
						}
					}
				}
			}
		}
	}
}
