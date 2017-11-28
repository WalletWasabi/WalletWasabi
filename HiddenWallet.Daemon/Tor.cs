using DotNetTor.SocksPort;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HiddenWallet.Daemon
{
	public static class Tor
	{
		public static Process TorProcess = null;
		public const int SOCKSPort = 37121;
		public const int ControlPort = 37122;
		public const string HashedControlPassword = "16:0978DBAF70EEB5C46063F3F6FD8CBC7A86DF70D2206916C1E2AE29EAF6";
		public const string DataDirectory = "TorData";
		public static string TorArguments => $"SOCKSPort {SOCKSPort} ControlPort {ControlPort} HashedControlPassword {HashedControlPassword} DataDirectory {DataDirectory}";
		public static SocksPortHandler SocksPortHandler = new SocksPortHandler("127.0.0.1", socksPort: SOCKSPort);
		public static DotNetTor.ControlPort.Client ControlPortClient = new DotNetTor.ControlPort.Client("127.0.0.1", controlPort: ControlPort, password: "ILoveBitcoin21");
		public static TorState State = TorState.NotStarted;
		public static CancellationTokenSource CircuitEstabilishingJobCancel = new CancellationTokenSource();

		public static async Task MakeSureCircuitEstabilishedAsync()
		{
			var ctsToken = CircuitEstabilishingJobCancel.Token;
			State = TorState.NotStarted;
			TryBroadcast();

			while (true)
			{
				try
				{
					if (ctsToken.IsCancellationRequested) return;

					try
					{
						var estabilished = await ControlPortClient.IsCircuitEstabilishedAsync().WithTimeoutAsync(TimeSpan.FromSeconds(3));
						if (ctsToken.IsCancellationRequested) return;

						if (estabilished)
						{
							State = TorState.CircuitEstabilished;
							TryBroadcast();
						}
						else
						{
							State = TorState.EstabilishingCircuit;
							TryBroadcast();
						}
					}
					catch (Exception ex)
					{
						if (ex is TimeoutException || ex.InnerException is TimeoutException)
						{
							// Tor messed up something internally, this sometimes happens when it creates new datadir (at first launch)
							// Restarting to solves the issue
							await RestartAsync();
						}
						if (ctsToken.IsCancellationRequested) return;
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine("Ignoring Tor exception:");
					Debug.WriteLine(ex);
				}

				if (State == TorState.CircuitEstabilished)
				{
					return;
				}
				var wait = TimeSpan.FromSeconds(3);
				await Task.Delay(wait, ctsToken).ContinueWith(tsk => { });
			}
		}

		public static async Task RestartAsync()
		{
			Kill();

			await Task.Delay(3000).ContinueWith(tsk => { });

			try
			{
				Console.WriteLine("Starting Tor process...");
				TorProcess.Start();

				CircuitEstabilishingJobCancel = new CancellationTokenSource();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
				MakeSureCircuitEstabilishedAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
				State = TorState.NotStarted;
				TryBroadcast();
			}
		}

		public static void Kill()
		{
			CircuitEstabilishingJobCancel.Cancel();
			State = TorState.NotStarted;
			TryBroadcast();
			if (TorProcess != null && !TorProcess.HasExited)
			{
				Console.WriteLine("Terminating Tor process");
				TorProcess.Kill();
			}
		}

		private static void TryBroadcast()
		{
			try
			{
				NotificationBroadcaster.Instance.BroadcastTorState(State.ToString());
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}
		}

		public enum TorState
		{
			NotStarted,
			EstabilishingCircuit,
			CircuitEstabilished
		}
	}
}
