using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Configuration;
using WalletWasabi.BitcoinCore.Endpointing;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.BitcoinCore
{
	public class CoreNode
	{
		public EndPoint P2pEndPoint { get; private set; }
		public EndPoint RpcEndPoint { get; private set; }
		public RPCClient RpcClient { get; private set; }
		private BitcoindProcessBridge Bridge { get; set; }
		public Process Process { get; private set; }
		public string DataDir { get; private set; }
		public Network Network { get; private set; }

		public static async Task<CoreNode> CreateAsync(CoreNodeParams coreNodeParams)
		{
			Guard.NotNull(nameof(coreNodeParams), coreNodeParams);
			using (BenchmarkLogger.Measure())
			using (await KillerLock.LockAsync().ConfigureAwait(false))
			{
				var coreNode = new CoreNode();
				coreNode.DataDir = coreNodeParams.DataDir;
				coreNode.Network = coreNodeParams.Network;

				var configPath = Path.Combine(coreNode.DataDir, "bitcoin.conf");
				var config = new CoreConfig();
				if (File.Exists(configPath))
				{
					var configString = await File.ReadAllTextAsync(configPath).ConfigureAwait(false);
					config.TryAdd(configString);
				}

				if (coreNodeParams.TryRestart)
				{
					await TryStopNoLockAsync(coreNode.Network, coreNode.DataDir, deleteDataDir: coreNodeParams.TryDeleteDataDir).ConfigureAwait(false);
				}
				IoHelpers.EnsureDirectoryExists(coreNode.DataDir);

				var configDic = config.ToDictionary();
				string rpcUser = null;
				string rpcPassword = null;
				EndPoint whitebind = null;
				string rpcHost = null;
				int? rpcPort = null;
				foreach (var networkPrefixWithDot in NetworkTranslator.GetConfigPrefixesWithDots(coreNode.Network))
				{
					var ru = configDic.TryGet($"{networkPrefixWithDot}rpcuser");
					var rp = configDic.TryGet($"{networkPrefixWithDot}rpcpassword");
					var wbs = configDic.TryGet($"{networkPrefixWithDot}whitebind");
					var rpst = configDic.TryGet($"{networkPrefixWithDot}rpchost");
					var rpts = configDic.TryGet($"{networkPrefixWithDot}rpcport");

					if (ru != null)
					{
						rpcUser = ru;
					}
					if (rp != null)
					{
						rpcUser = rp;
					}
					if (wbs != null && EndPointParser.TryParse(wbs, coreNode.Network.DefaultPort, out EndPoint wb))
					{
						whitebind = wb;
					}
					if (rpst != null)
					{
						rpcHost = rpst;
					}
					if (rpts != null && int.TryParse(rpts, out int rpt))
					{
						rpcPort = rpt;
					}
				}
				rpcUser ??= Encoders.Hex.EncodeData(RandomUtils.GetBytes(21));
				rpcPassword ??= Encoders.Hex.EncodeData(RandomUtils.GetBytes(21));
				var creds = new NetworkCredential(rpcUser, rpcPassword);

				coreNode.P2pEndPoint = whitebind ?? coreNodeParams.P2pEndPointStrategy.EndPoint;
				rpcHost ??= coreNodeParams.RpcEndPointStrategy.EndPoint.GetHostOrDefault();
				rpcPort ??= coreNodeParams.RpcEndPointStrategy.EndPoint.GetPortOrDefault();
				EndPointParser.TryParse($"{rpcHost}:{rpcPort}", coreNode.Network.RPCPort, out EndPoint rpce);
				coreNode.RpcEndPoint = rpce;

				coreNode.RpcClient = new RPCClient($"{creds.UserName}:{creds.Password}", coreNode.RpcEndPoint.ToString(coreNode.Network.DefaultPort), coreNode.Network);
				var pidFileName = "bitcoind.pid";

				var configPrefix = NetworkTranslator.GetConfigPrefix(coreNode.Network);
				var desiredConfigString =
@$"{configPrefix}.server		= 1
{configPrefix}.txindex			= 1
{configPrefix}.whitebind		= {coreNode.P2pEndPoint.ToString(coreNode.Network.DefaultPort)}
{configPrefix}.rpcuser			= {coreNode.RpcClient.CredentialString.UserPassword.UserName}
{configPrefix}.rpcpassword		= {coreNode.RpcClient.CredentialString.UserPassword.Password}
{configPrefix}.rpchost			= {coreNode.RpcEndPoint.GetHostOrDefault()}
{configPrefix}.rpcport			= {coreNode.RpcEndPoint.GetPortOrDefault()}
{configPrefix}.pid				= {pidFileName}";

				config.AddOrUpdate(desiredConfigString);
				await File.WriteAllTextAsync(configPath, config.ToString());
				var configFileName = Path.GetFileName(configPath);

				coreNode.Bridge = new BitcoindProcessBridge();
				coreNode.Process = coreNode.Bridge.Start($"-{coreNode.Network.ToString().ToLowerInvariant()} -conf={configFileName} -datadir={coreNode.DataDir} -printtoconsole=0", false);
				string pidFile = Path.Combine(coreNode.DataDir, NetworkTranslator.GetDataDirPrefix(coreNode.Network), pidFileName);
				if (!File.Exists(pidFile))
				{
					IoHelpers.EnsureDirectoryExists(Path.Combine(coreNode.DataDir, NetworkTranslator.GetDataDirPrefix(coreNode.Network)));
					await File.WriteAllTextAsync(pidFile, coreNode.Process.Id.ToString());
				}

				while (true)
				{
					try
					{
						await coreNode.RpcClient.GetBlockHashAsync(0).ConfigureAwait(false);
						break;
					}
					catch (Exception ex)
					{
						Logger.LogTrace(ex);
					}
					if (coreNode.Process is null || coreNode.Process.HasExited)
					{
						break;
					}
				}

				return coreNode;
			}
		}

		public static async Task<Version> GetVersionAsync(CancellationToken cancel)
		{
			var arguments = "-version";
			var bridge = new BitcoindProcessBridge();
			var (responseString, exitCode) = await bridge.SendCommandAsync(arguments, false, cancel).ConfigureAwait(false);

			if (exitCode != 0)
			{
				throw new BitcoindException($"'bitcoind {arguments}' exited with incorrect exit code: {exitCode}.");
			}
			var firstLine = responseString.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).First();
			var versionString = firstLine.TrimStart("Bitcoin Core Daemon version v", StringComparison.OrdinalIgnoreCase);
			var version = new Version(versionString);
			return version;
		}

		public async Task<Node> CreateP2pNodeAsync()
		{
			return await Node.ConnectAsync(Network, P2pEndPoint).ConfigureAwait(false);
		}

		public async Task<IEnumerable<Block>> GenerateAsync(int blockCount)
		{
			var blocks = await RpcClient.GenerateAsync(blockCount).ConfigureAwait(false);
			var rpc = RpcClient.PrepareBatch();
			var tasks = blocks.Select(b => rpc.GetBlockAsync(b));
			rpc.SendBatch();
			return await Task.WhenAll(tasks).ConfigureAwait(false);
		}

		public async Task StopAsync(bool deleteDataDir)
		{
			await TryStopAsync(Network, DataDir, deleteDataDir).ConfigureAwait(false);
		}

		private static AsyncLock KillerLock { get; } = new AsyncLock();

		public static async Task TryStopAsync(Network network, string dataDir, bool deleteDataDir)
		{
			using (await KillerLock.LockAsync().ConfigureAwait(false))
			{
				await TryStopNoLockAsync(network, dataDir, deleteDataDir).ConfigureAwait(false);
			}
		}

		public static async Task TryStopNoLockAsync(Network network, string dataDir, bool deleteDataDir)
		{
			dataDir = Guard.NotNullOrEmptyOrWhitespace(nameof(dataDir), dataDir);
			network = Guard.NotNull(nameof(network), network);

			try
			{
				var reasonableCoreShutdownTimeout = TimeSpan.FromSeconds(21);
				var configPath = Path.Combine(dataDir, "bitcoin.conf");
				var config = new CoreConfig();
				if (File.Exists(configPath))
				{
					var configString = await File.ReadAllTextAsync(configPath).ConfigureAwait(false);
					config.TryAdd(configString);

					var foundConfigDic = config.ToDictionary();

					var networkEntry = NetworkTranslator.GetConfigPrefix(network);
					var rpcPortString = foundConfigDic.TryGet($"{networkEntry}.rpcport");
					var rpcUser = foundConfigDic.TryGet($"{networkEntry}.rpcuser");
					var rpcPassword = foundConfigDic.TryGet($"{networkEntry}.rpcpassword");
					var pidFileName = foundConfigDic.TryGet($"{networkEntry}.pid");

					var credentials = new NetworkCredential(rpcUser, rpcPassword);
					var rpc = new RPCClient(credentials, new Uri("http://127.0.0.1:" + rpcPortString + "/"), network);
					await rpc.StopAsync().ConfigureAwait(false);

					var pidFile = Path.Combine(dataDir, NetworkTranslator.GetDataDirPrefix(network), pidFileName);
					using CancellationTokenSource cts = new CancellationTokenSource(reasonableCoreShutdownTimeout);
					if (File.Exists(pidFile))
					{
						var pid = await File.ReadAllTextAsync(pidFile).ConfigureAwait(false);
						using var process = Process.GetProcessById(int.Parse(pid));
						await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
					}
					else
					{
						var allProcesses = Process.GetProcesses();
						var bitcoindProcesses = allProcesses.Where(x => x.ProcessName.Contains("bitcoind"));
						if (bitcoindProcesses.Count() == 1)
						{
							var bitcoind = bitcoindProcesses.First();
							await bitcoind.WaitForExitAsync(cts.Token).ConfigureAwait(false);
						}
					}
				}

				if (deleteDataDir)
				{
					await IoHelpers.DeleteRecursivelyWithMagicDustAsync(dataDir).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}
	}
}
