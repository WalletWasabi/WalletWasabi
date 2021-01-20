using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor
{
	/// <summary>
	/// Monitors Tor process.
	/// <para>
	/// Monitor checks periodically (every 3 seconds) whether Tor reported an error or not.
	/// Recovery process is simply an attempt to restart Tor process again. This is no-op when Tor is running.
	/// </para>
	/// </summary>
	public class TorMonitor : PeriodicRunner
	{
		/// <summary></summary>
		private static readonly TimeSpan MisbehaviorCheckPeriod = TimeSpan.FromSeconds(7);

		/// <summary>
		/// Creates a new instance of the object.
		/// </summary>
		public TorMonitor(TimeSpan period, Uri fallBackTestRequestUri, TorProcessManager torProcessManager, TorSocks5ClientPool pool) : base(period)
		{
			FallBackTestRequestUri = fallBackTestRequestUri;
			TorProcessManager = torProcessManager;
			Pool = pool;
		}

		public static bool RequestFallbackAddressUsage { get; private set; } = false;
		private Uri FallBackTestRequestUri { get; }
		private TorProcessManager TorProcessManager { get; }
		private TorSocks5ClientPool Pool { get; }

		/// <inheritdoc/>
		protected override async Task ActionAsync(CancellationToken token)
		{
			if (Pool.TorDoesntWorkSince is { }) // If Tor misbehaves.
			{
				TimeSpan torMisbehavedFor = DateTimeOffset.UtcNow - Pool.TorDoesntWorkSince ?? TimeSpan.Zero;

				if (torMisbehavedFor > MisbehaviorCheckPeriod)
				{
					if (Pool.LatestTorException is TorConnectCommandFailedException torEx)
					{
						if (torEx.RepField == RepField.HostUnreachable)
						{
							Uri baseUri = new Uri($"{FallBackTestRequestUri.Scheme}://{FallBackTestRequestUri.DnsSafeHost}");

							var client = new TorHttpClient(Pool, () => baseUri);
							var message = new HttpRequestMessage(HttpMethod.Get, FallBackTestRequestUri);
							await client.SendAsync(message, token).ConfigureAwait(false);

							// Check if it changed in the meantime...
							if (Pool.LatestTorException is TorConnectCommandFailedException torEx2 && torEx2.RepField == RepField.HostUnreachable)
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
