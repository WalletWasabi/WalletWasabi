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

		public BitcoindRpcProcessBridge(RPCClient rpc, string dataDir, bool printToConsole) : base()
		{
			RpcClient = Guard.NotNull(nameof(rpc), rpc);
			Network = RpcClient.Network;
			DataDir = Guard.NotNullOrEmptyOrWhitespace(nameof(dataDir), dataDir);
			PrintToConsole = printToConsole;
			PidFile = new PidFile(DataDir, Network);
		}

		public async Task StartAsync()
		{
			var ptcv = PrintToConsole ? 1 : 0;
			using var process = Start($"{NetworkTranslator.GetCommandLineArguments(Network)} -datadir={DataDir} -printtoconsole={ptcv}", false);

			await PidFile.SerializeAsync(process.Id).ConfigureAwait(false);

			while (true)
			{
				var ex = await RpcClient.TestAsync().ConfigureAwait(false);
				if (ex is null)
				{
					break;
				}

				if (process is null || process.HasExited)
				{
					throw ex;
				}
			}
		}

		public async Task StopAsync()
		{
			try
			{
				var reasonableCoreShutdownTimeout = TimeSpan.FromSeconds(21);

				using CancellationTokenSource cts = new CancellationTokenSource(reasonableCoreShutdownTimeout);
				var pid = await PidFile.TryReadAsync().ConfigureAwait(false);

				if (pid.HasValue)
				{
					using Process process = Process.GetProcessById(pid.Value);
					try
					{
						await RpcClient.StopAsync().ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						process.Kill();
						Logger.LogDebug(ex);
					}
					await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
				}
			}
			finally
			{
				PidFile.TryDelete();
			}
		}
	}
}
