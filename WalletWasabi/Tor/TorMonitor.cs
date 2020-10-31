using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Exceptions;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor
{
	/// <summary>
	/// Monitors Tor process.
	/// </summary>
	public class TorMonitor
	{
		public static readonly TimeSpan TorMisbehaviorCheckPeriod = TimeSpan.FromSeconds(3);
		public static readonly TimeSpan CheckIfRunningAfterTorMisbehavedFor = TimeSpan.FromSeconds(7);

		private const long StateNotStarted = 0;

		private const long StateRunning = 1;

		private const long StateStopping = 2;

		private const long StateStopped = 3;

		/// <summary>
		/// Value can be any of: <see cref="StateNotStarted"/>, <see cref="StateRunning"/>, <see cref="StateStopping"/> and <see cref="StateStopped"/>.
		/// </summary>
		private long _monitorState;

		/// <summary>
		/// Creates a new instance of the object.
		/// </summary>
		public TorMonitor(TorProcessManager torProcessManager)
		{
			_monitorState = StateNotStarted;
			MonitorCts = new CancellationTokenSource();
			TorProcessManager = torProcessManager;
		}

		public static bool RequestFallbackAddressUsage { get; private set; } = false;

		private CancellationTokenSource MonitorCts { get; set; }

		private TorProcessManager TorProcessManager { get; }

		public bool IsRunning => Interlocked.Read(ref _monitorState) == StateRunning;

		public void StartMonitor(Uri fallBackTestRequestUri, EndPoint torSocks5EndPoint)
		{
			Logger.LogInfo("Starting Tor monitor...");
			if (Interlocked.CompareExchange(ref _monitorState, StateRunning, StateNotStarted) != StateNotStarted)
			{
				return;
			}

			Task.Run(async () =>
			{
				try
				{
					while (IsRunning)
					{
						try
						{
							await Task.Delay(TorMisbehaviorCheckPeriod, MonitorCts.Token).ConfigureAwait(false);

							if (TorHttpClient.TorDoesntWorkSince is { }) // If Tor misbehaves.
							{
								TimeSpan torMisbehavedFor = DateTimeOffset.UtcNow - TorHttpClient.TorDoesntWorkSince ?? TimeSpan.Zero;

								if (torMisbehavedFor > CheckIfRunningAfterTorMisbehavedFor)
								{
									if (TorHttpClient.LatestTorException is TorSocks5FailureResponseException torEx)
									{
										if (torEx.RepField == RepField.HostUnreachable)
										{
											Uri baseUri = new Uri($"{fallBackTestRequestUri.Scheme}://{fallBackTestRequestUri.DnsSafeHost}");
											using (var client = new TorHttpClient(baseUri, torSocks5EndPoint))
											{
												var message = new HttpRequestMessage(HttpMethod.Get, fallBackTestRequestUri);
												await client.SendAsync(message, MonitorCts.Token).ConfigureAwait(false);
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
										await TorProcessManager.StartAsync(ensureRunning: true).ConfigureAwait(false); // Try starting Tor, if it does not work it'll be another issue.
									}
								}
							}
						}
						catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException || ex is TimeoutException)
						{
							Logger.LogTrace(ex);
						}
						catch (Exception ex)
						{
							Logger.LogDebug(ex);
						}
					}
				}
				finally
				{
					Interlocked.CompareExchange(ref _monitorState, StateStopped, StateStopping); // If IsStopping, make it stopped.
				}
			});
		}

		public async Task StopAsync()
		{
			Interlocked.CompareExchange(ref _monitorState, StateStopping, StateRunning); // If running, make it stopping.

			MonitorCts?.Cancel();
			while (Interlocked.CompareExchange(ref _monitorState, StateStopped, StateNotStarted) == StateStopping)
			{
				await Task.Delay(50).ConfigureAwait(false);
			}
			MonitorCts?.Dispose();
		}
	}
}
