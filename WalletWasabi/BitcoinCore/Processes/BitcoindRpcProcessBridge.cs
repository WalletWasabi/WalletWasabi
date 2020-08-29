using NBitcoin;
using NBitcoin.RPC;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Configuration;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;

namespace WalletWasabi.BitcoinCore.Processes
{
	public class BitcoindRpcProcessBridge
	{
		public const string PidFileName = "bitcoin.pid";

		public BitcoindRpcProcessBridge(IRPCClient rpc, string dataDir, bool printToConsole)
		{
			RpcClient = Guard.NotNull(nameof(rpc), rpc);
			Network = RpcClient.Network;
			DataDir = Guard.NotNullOrEmptyOrWhitespace(nameof(dataDir), dataDir);
			PrintToConsole = printToConsole;
			PidFile = new PidFile(Path.Combine(DataDir, NetworkTranslator.GetDataDirPrefix(Network)), PidFileName);
			CachedPid = null;
			Process = null;
 		}

		private ProcessAsync? Process { get; set; }
		public Network Network { get; }
		public IRPCClient RpcClient { get; set; }
		public string DataDir { get; }
		public bool PrintToConsole { get; }
		public PidFile PidFile { get; }
		private int? CachedPid { get; set; }

		/// <summary>
		/// This method can be called only once.
		/// </summary>
		public async Task StartAsync(CancellationToken cancel)
		{
			var ptcv = PrintToConsole ? 1 : 0;
			string processPath = MicroserviceHelpers.GetBinaryPath("bitcoind");
			string args = $"{NetworkTranslator.GetCommandLineArguments(Network)} -datadir=\"{DataDir}\" -printtoconsole={ptcv}";

			Process = new ProcessAsync(ProcessStartInfoFactory.Make(processPath, args));
			Process.Start();

			await PidFile.WriteFileAsync(Process.Id).ConfigureAwait(false);
			CachedPid = Process.Id;

			string? latestFailureMessage = null;

			try
			{
				// Try to connect to bitcoin daemon RPC until we succeed.
				while (Process is { })
				{
					var ex = await RpcClient.TestAsync().ConfigureAwait(false);
					if (ex is null)
					{
						Logger.LogInfo($"RPC connection is successfully established.");
						break;
					}
					else if (latestFailureMessage != ex.Message)
					{
						latestFailureMessage = ex.Message;
						Logger.LogInfo($"{Constants.BuiltinBitcoinNodeName} is not yet ready... Reason: {latestFailureMessage}");
					}

					if (Process.HasExited)
					{
						throw new BitcoindException($"Failed to start daemon, location: '{Process.StartInfo.FileName} {Process.StartInfo.Arguments}'", ex);
					}

					if (cancel.IsCancellationRequested)
					{
						await StopAsync(true).ConfigureAwait(false);
						cancel.ThrowIfCancellationRequested();
					}

					await Task.Delay(100).ConfigureAwait(false); // So to leave some breathing room before the next check.
				}
			}
			catch (Exception)
			{
				Process?.Dispose();
				throw;
			}
		}

		/// <summary>
		/// Stops bitcoin daemon process when PID file exists.
		/// </summary>
		/// <remarks>If there is not PID file, no process is stopped.</remarks>
		/// <param name="onlyOwned">Only stop if this node owns the process.</param>
		public async Task StopAsync(bool onlyOwned)
		{
			Logger.LogDebug($"> {nameof(onlyOwned)}={onlyOwned}");

			if (Process is null)
			{
				Logger.LogDebug("< Process is null.");
				return;
			}

			ProcessAsync process = Process; // process is guaranteed to be non-null at this point.

			var rpcRan = false;
			try
			{
				var reasonableCoreShutdownTimeout = TimeSpan.FromSeconds(21);

				using var cts = new CancellationTokenSource(reasonableCoreShutdownTimeout);
				int? pid = await PidFile.TryReadAsync().ConfigureAwait(false);

				// If the cached PID is PID, then we own the process.
				if (pid.HasValue && (!onlyOwned || CachedPid == pid))
				{
					try
					{
						try
						{
							await RpcClient.StopAsync().ConfigureAwait(false);
							rpcRan = true;
						}
						catch (Exception ex)
						{
							Logger.LogWarning(ex);
							process.Kill();
						}

						await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
					}
					finally
					{
						process.Dispose();
						Process = null;
						PidFile.TryDelete();
					}
				}
			}
			catch
			{
				if (!onlyOwned && !rpcRan)
				{
					await RpcClient.StopAsync().ConfigureAwait(false);
				}
			}

			Logger.LogDebug("<");
		}
	}
}
