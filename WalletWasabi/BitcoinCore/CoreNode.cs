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

		public CoreConfig Config { get; private set; }

		public static async Task<CoreNode> CreateAsync(CoreNodeParams coreNodeParams)
		{
			Guard.NotNull(nameof(coreNodeParams), coreNodeParams);
			using (BenchmarkLogger.Measure())
			{
				var coreNode = new CoreNode();
				coreNode.DataDir = coreNodeParams.DataDir;
				coreNode.Network = coreNodeParams.Network;

				var configPath = Path.Combine(coreNode.DataDir, "bitcoin.conf");
				coreNode.Config = new CoreConfig();
				if (File.Exists(configPath))
				{
					var configString = await File.ReadAllTextAsync(configPath).ConfigureAwait(false);
					coreNode.Config.TryAdd(configString);
				}

				var configDic = coreNode.Config.ToDictionary();
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
						rpcPassword = rp;
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

				if (coreNodeParams.TryRestart && await coreNode.TryStopAsync().ConfigureAwait(false) && coreNodeParams.TryDeleteDataDir)
				{
					await IoHelpers.DeleteRecursivelyWithMagicDustAsync(coreNode.DataDir).ConfigureAwait(false);
				}

				IoHelpers.EnsureDirectoryExists(coreNode.DataDir);

				var configPrefix = NetworkTranslator.GetConfigPrefix(coreNode.Network);
				var desiredConfigLines = new List<string>()
				{
					$"{configPrefix}.server			= 1",
					$"{configPrefix}.txindex		= 1",
					$"{configPrefix}.whitebind		= {coreNode.P2pEndPoint.ToString(coreNode.Network.DefaultPort)}",
					$"{configPrefix}.rpcuser		= {coreNode.RpcClient.CredentialString.UserPassword.UserName}",
					$"{configPrefix}.rpcpassword	= {coreNode.RpcClient.CredentialString.UserPassword.Password}",
					$"{configPrefix}.rpchost		= {coreNode.RpcEndPoint.GetHostOrDefault()}",
					$"{configPrefix}.rpcport		= {coreNode.RpcEndPoint.GetPortOrDefault()}"
				};

				var sectionComment = $"# The following configuration options were added or modified by Wasabi Wallet.";
				// If the comment is not already present.
				// And there would be new config entries added.
				var throwAwayConfig = new CoreConfig(coreNode.Config);
				throwAwayConfig.AddOrUpdate(string.Join(Environment.NewLine, desiredConfigLines));
				if (!coreNode.Config.ToString().Contains(sectionComment, StringComparison.Ordinal)
					&& throwAwayConfig.Count != coreNode.Config.Count)
				{
					desiredConfigLines.Insert(0, sectionComment);
				}

				if (coreNode.Config.AddOrUpdate(string.Join(Environment.NewLine, desiredConfigLines)))
				{
					await File.WriteAllTextAsync(configPath, coreNode.Config.ToString());
				}
				var configFileName = Path.GetFileName(configPath);

				coreNode.Bridge = new BitcoindProcessBridge();
				coreNode.Process = coreNode.Bridge.Start($"-{coreNode.Network.ToString().ToLowerInvariant()} -conf={configFileName} -datadir={coreNode.DataDir} -printtoconsole=0", false);

				var pidFile = new PidFile(coreNode.DataDir, coreNode.Network);
				await pidFile.SerializeAsync(coreNode.Process.Id).ConfigureAwait(false);

				while (true)
				{
					try
					{
						await coreNode.RpcClient.GetBlockchainInfoAsync().ConfigureAwait(false);
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

		public async Task<bool> TryStopAsync()
		{
			try
			{
				var reasonableCoreShutdownTimeout = TimeSpan.FromSeconds(21);

				var foundConfigDic = Config.ToDictionary();

				using CancellationTokenSource cts = new CancellationTokenSource(reasonableCoreShutdownTimeout);
				var pidFile = new PidFile(DataDir, Network);
				var pid = await pidFile.TryReadAsync().ConfigureAwait(false);

				if (pid.HasValue)
				{
					using Process process = Process.GetProcessById(pid.Value);
					var waitForExit = process.WaitForExitAsync(cts.Token);
					try
					{
						await RpcClient.StopAsync().ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						process.Kill();
						Logger.LogDebug(ex);
					}
					await waitForExit.ConfigureAwait(false);
					return true;
				}
				else
				{
					await RpcClient.StopAsync().ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}

			return false;
		}
	}
}
