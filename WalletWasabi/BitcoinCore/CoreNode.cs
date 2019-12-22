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
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.BitcoinCore.Processes;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.P2p;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Stores;

namespace WalletWasabi.BitcoinCore
{
	public class CoreNode
	{
		public EndPoint P2pEndPoint { get; private set; }
		public EndPoint RpcEndPoint { get; private set; }
		public RPCClient RpcClient { get; private set; }
		private BitcoindRpcProcessBridge Bridge { get; set; }
		public HostedServices HostedServices { get; private set; }
		public string DataDir { get; private set; }
		public Network Network { get; private set; }
		public MempoolService MempoolService { get; private set; }

		public CoreConfig Config { get; private set; }
		public P2pNode P2pNode { get; private set; }

		public static async Task<CoreNode> CreateAsync(CoreNodeParams coreNodeParams, CancellationToken cancel)
		{
			Guard.NotNull(nameof(coreNodeParams), coreNodeParams);
			using (BenchmarkLogger.Measure())
			{
				var coreNode = new CoreNode();
				coreNode.HostedServices = coreNodeParams.HostedServices;
				coreNode.DataDir = coreNodeParams.DataDir;
				coreNode.Network = coreNodeParams.Network;
				coreNode.MempoolService = coreNodeParams.MempoolService;

				var configPath = Path.Combine(coreNode.DataDir, "bitcoin.conf");
				coreNode.Config = new CoreConfig();
				if (File.Exists(configPath))
				{
					var configString = await File.ReadAllTextAsync(configPath).ConfigureAwait(false);
					coreNode.Config.TryAdd(configString);
				}
				cancel.ThrowIfCancellationRequested();

				var configDic = coreNode.Config.ToDictionary();
				string rpcUser = null;
				string rpcPassword = null;
				string whitebindPermissions = null;
				EndPoint whitebindEndPoint = null;
				string rpcHost = null;
				int? rpcPort = null;
				string rpcCookieFilePath = null;
				foreach (var networkPrefixWithDot in NetworkTranslator.GetConfigPrefixesWithDots(coreNode.Network))
				{
					var rpcc = configDic.TryGet($"{networkPrefixWithDot}rpccookiefile");
					var ru = configDic.TryGet($"{networkPrefixWithDot}rpcuser");
					var rp = configDic.TryGet($"{networkPrefixWithDot}rpcpassword");
					var wbs = configDic.TryGet($"{networkPrefixWithDot}whitebind");
					var rpst = configDic.TryGet($"{networkPrefixWithDot}rpchost");
					var rpts = configDic.TryGet($"{networkPrefixWithDot}rpcport");

					if (rpcc is { })
					{
						rpcCookieFilePath = rpcc;
					}
					if (ru is { })
					{
						rpcUser = ru;
					}
					if (rp is { })
					{
						rpcPassword = rp;
					}

					// https://github.com/bitcoin/bitcoin/pull/16248
					var wbsParts = wbs?.Split('@');
					if (wbsParts is { })
					{
						if (EndPointParser.TryParse(wbsParts.LastOrDefault(), coreNode.Network.DefaultPort, out EndPoint wbEndPoint))
						{
							whitebindEndPoint = wbEndPoint;
						}

						if (wbsParts.Length > 1)
						{
							whitebindPermissions = wbsParts.First();
						}
					}

					if (rpst is { })
					{
						rpcHost = rpst;
					}
					if (rpts is { } && int.TryParse(rpts, out int rpt))
					{
						rpcPort = rpt;
					}
				}

				string authString;
				bool cookieAuth = rpcCookieFilePath is { };
				if (cookieAuth)
				{
					authString = $"cookiefile={rpcCookieFilePath}";
				}
				else
				{
					rpcUser ??= Encoders.Hex.EncodeData(RandomUtils.GetBytes(21));
					rpcPassword ??= Encoders.Hex.EncodeData(RandomUtils.GetBytes(21));
					authString = $"{rpcUser}:{rpcPassword}";
				}

				coreNode.P2pEndPoint = whitebindEndPoint ?? coreNodeParams.P2pEndPointStrategy.EndPoint;
				rpcHost ??= coreNodeParams.RpcEndPointStrategy.EndPoint.GetHostOrDefault();
				rpcPort ??= coreNodeParams.RpcEndPointStrategy.EndPoint.GetPortOrDefault();
				EndPointParser.TryParse($"{rpcHost}:{rpcPort}", coreNode.Network.RPCPort, out EndPoint rpce);
				coreNode.RpcEndPoint = rpce;

				coreNode.RpcClient = new RPCClient($"{authString}", coreNode.RpcEndPoint.ToString(coreNode.Network.DefaultPort), coreNode.Network);

				if (coreNodeParams.TryRestart)
				{
					await coreNode.TryStopAsync(false).ConfigureAwait(false);
				}
				cancel.ThrowIfCancellationRequested();

				if (coreNodeParams.TryDeleteDataDir)
				{
					await IoHelpers.DeleteRecursivelyWithMagicDustAsync(coreNode.DataDir).ConfigureAwait(false);
				}
				cancel.ThrowIfCancellationRequested();

				IoHelpers.EnsureDirectoryExists(coreNode.DataDir);

				var configPrefix = NetworkTranslator.GetConfigPrefix(coreNode.Network);
				var whiteBindPermissionsPart = whitebindPermissions is { } ? $"{whitebindPermissions}@" : "";
				var desiredConfigLines = new List<string>()
				{
					$"{configPrefix}.server			= 1",
					$"{configPrefix}.listen			= 1",
					$"{configPrefix}.whitebind		= {whiteBindPermissionsPart}{coreNode.P2pEndPoint.ToString(coreNode.Network.DefaultPort)}",
					$"{configPrefix}.rpchost		= {coreNode.RpcEndPoint.GetHostOrDefault()}",
					$"{configPrefix}.rpcport		= {coreNode.RpcEndPoint.GetPortOrDefault()}"
				};

				if (!cookieAuth)
				{
					desiredConfigLines.Add($"{configPrefix}.rpcuser		= {coreNode.RpcClient.CredentialString.UserPassword.UserName}");
					desiredConfigLines.Add($"{configPrefix}.rpcpassword	= {coreNode.RpcClient.CredentialString.UserPassword.Password}");
				}

				if (coreNodeParams.TxIndex is { })
				{
					desiredConfigLines.Add($"{configPrefix}.txindex = {coreNodeParams.TxIndex}");
				}

				if (coreNodeParams.Prune is { })
				{
					desiredConfigLines.Add($"{configPrefix}.prune = {coreNodeParams.Prune}");
				}

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

				if (coreNode.Config.AddOrUpdate(string.Join(Environment.NewLine, desiredConfigLines))
					|| !File.Exists(configPath))
				{
					IoHelpers.EnsureContainingDirectoryExists(configPath);
					await File.WriteAllTextAsync(configPath, coreNode.Config.ToString()).ConfigureAwait(false);
				}
				cancel.ThrowIfCancellationRequested();

				// If it isn't already running, then we run it.
				if (await coreNode.RpcClient.TestAsync().ConfigureAwait(false) is null)
				{
					Logger.LogInfo("Bitcoin Core is already running.");
				}
				else
				{
					coreNode.Bridge = new BitcoindRpcProcessBridge(coreNode.RpcClient, coreNode.DataDir, printToConsole: false);
					await coreNode.Bridge.StartAsync(cancel).ConfigureAwait(false);
					Logger.LogInfo("Started Bitcoin Core.");
				}
				cancel.ThrowIfCancellationRequested();

				coreNode.P2pNode = new P2pNode(coreNode.Network, coreNode.P2pEndPoint, coreNode.MempoolService, coreNodeParams.UserAgent);
				await coreNode.P2pNode.ConnectAsync(cancel).ConfigureAwait(false);
				cancel.ThrowIfCancellationRequested();

				coreNode.HostedServices.Register(new BlockNotifier(TimeSpan.FromSeconds(7), new RpcWrappedClient(coreNode.RpcClient), coreNode.P2pNode), "Block Notifier");
				coreNode.HostedServices.Register(new RpcMonitor(TimeSpan.FromSeconds(7), coreNode.RpcClient), "RPC Monitor");
				coreNode.HostedServices.Register(new RpcFeeProvider(TimeSpan.FromMinutes(1), coreNode.RpcClient), "RPC Fee Provider");

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

		public async Task<Node> CreateNewP2pNodeAsync()
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

		public async Task DisposeAsync()
		{
			var p2pNode = P2pNode;
			if (p2pNode is { })
			{
				await p2pNode.DisposeAsync().ConfigureAwait(false);
			}
		}

		/// <param name="onlyOwned">Only stop if this node owns the process.</param>
		public async Task<bool> TryStopAsync(bool onlyOwned = true)
		{
			await DisposeAsync().ConfigureAwait(false);

			Exception exThrown = null;

			BitcoindRpcProcessBridge bridge = null;
			if (Bridge is { })
			{
				bridge = Bridge;
			}
			else if (!onlyOwned)
			{
				bridge = new BitcoindRpcProcessBridge(RpcClient, DataDir, printToConsole: false);
			}

			if (bridge is { })
			{
				try
				{
					await bridge.StopAsync(onlyOwned).ConfigureAwait(false);
					Logger.LogInfo("Stopped.");
					return true;
				}
				catch (Exception ex)
				{
					exThrown = ex;
				}
			}

			Logger.LogInfo("Did not stop Bitcoin Core. Reason:");
			if (exThrown is null)
			{
				Logger.LogInfo("Bitcoin Core was started externally.");
			}
			else
			{
				Logger.LogWarning(exThrown);
			}
			return false;
		}
	}
}
