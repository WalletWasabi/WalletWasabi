using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Configuration;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;

namespace WalletWasabi.BitcoinCore.Processes
{
	public class BitcoindRpcProcessBridge : BitcoindProcessBridge
	{
		public Network Network { get; }
		public RPCClient RpcClient { get; set; }
		public string DataDir { get; }
		public bool PrintToConsole { get; }
		public PidFile PidFile { get; }
		private int? CachedPid { get; set; }

		public BitcoindRpcProcessBridge(RPCClient rpc, string dataDir, bool printToConsole) : base()
		{
			RpcClient = Guard.NotNull(nameof(rpc), rpc);
			Network = RpcClient.Network;
			DataDir = Guard.NotNullOrEmptyOrWhitespace(nameof(dataDir), dataDir);
			PrintToConsole = printToConsole;
			PidFile = new PidFile(DataDir, Network);
			CachedPid = null;
		}

		public async Task StartAsync(CancellationToken cancel)
		{
			var ptcv = PrintToConsole ? 1 : 0;
			using var process = Start($"{NetworkTranslator.GetCommandLineArguments(Network)} -datadir={DataDir} -printtoconsole={ptcv}", false);

			await PidFile.SerializeAsync(process.Id).ConfigureAwait(false);
			CachedPid = process.Id;

			string latestFailureMessage = null;
			while (true)
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
					Logger.LogInfo($"Bitcoin Core is not yet ready... Reason: {latestFailureMessage}");
				}

				if (process is null || process.HasExited)
				{
					throw ex;
				}

				if (cancel.IsCancellationRequested)
				{
					await StopAsync(true).ConfigureAwait(false);
					cancel.ThrowIfCancellationRequested();
				}

				await Task.Delay(100).ConfigureAwait(false); // So to leave some breathing room before the next check.
			}
		}

		/// <param name="onlyOwned">Only stop if this node owns the process.</param>
		public async Task StopAsync(bool onlyOwned)
		{
			var rpcRan = false;
			try
			{
				var reasonableCoreShutdownTimeout = TimeSpan.FromSeconds(21);

				using CancellationTokenSource cts = new CancellationTokenSource(reasonableCoreShutdownTimeout);
				int? pid = await PidFile.TryReadAsync().ConfigureAwait(false);

				// If the cached pid is pid, then we own the process.
				if (pid.HasValue && (!onlyOwned || CachedPid == pid))
				{
					try
					{
						using Process process = Process.GetProcessById(pid.Value);
						try
						{
							await RpcClient.StopAsync().ConfigureAwait(false);
							rpcRan = true;
						}
						catch (Exception ex)
						{
							process.Kill();
							Logger.LogDebug(ex);
						}
						await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
					}
					finally
					{
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
		}
	}
}
