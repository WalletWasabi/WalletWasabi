using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Configuration;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;

namespace WalletWasabi.BitcoinCore.Processes
{
	public class BitcoindRpcProcessBridge : ProcessBridge
	{
		public const string PidFileName = "bitcoin.pid";

		public BitcoindRpcProcessBridge(IRPCClient rpc, string dataDir, bool printToConsole) : base(MicroserviceHelpers.GetBinaryPath("bitcoind"))
		{
			RpcClient = Guard.NotNull(nameof(rpc), rpc);
			Network = RpcClient.Network;
			DataDir = Guard.NotNullOrEmptyOrWhitespace(nameof(dataDir), dataDir);
			PrintToConsole = printToConsole;
			PidFile = new PidFile(Path.Combine(DataDir, NetworkTranslator.GetDataDirPrefix(Network)), PidFileName);
			CachedPid = null;
		}

		public Network Network { get; }
		public IRPCClient RpcClient { get; set; }
		public string DataDir { get; }
		public bool PrintToConsole { get; }
		public PidFile PidFile { get; }
		private int? CachedPid { get; set; }

		private ProcessAsync _process = null;

		public async Task StartAsync(CancellationToken cancel)
		{
			var ptcv = PrintToConsole ? 1 : 0;
			string processPath = MicroserviceHelpers.GetBinaryPath("bitcoind");
			string args = $"{NetworkTranslator.GetCommandLineArguments(Network)} -datadir={DataDir} -printtoconsole={ptcv}";
			_process = new ProcessAsync(ProcessBuilder.BuildProcessInstance(processPath, args));
			_process.Start();

			await PidFile.WriteFileAsync(_process.Id).ConfigureAwait(false);
			CachedPid = _process.Id;

			string latestFailureMessage = null;

			try
			{
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

					if (_process is null || _process.HasExited)
					{
						throw new BitcoindException($"Failed to start daemon, location: '{_process?.StartInfo.FileName} {_process?.StartInfo.Arguments}'", ex);
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
				_process?.Dispose();
				throw;
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
								_process.Kill();
								Logger.LogDebug(ex);
							}
							await _process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
					}
					finally
					{
						_process?.Dispose();
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
