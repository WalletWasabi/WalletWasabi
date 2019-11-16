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
using WalletWasabi.BitcoinCore.Processes;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
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
		public string DataDir { get; private set; }
		public Network Network { get; private set; }
		public MempoolService MempoolService { get; private set; }

		public CoreConfig Config { get; private set; }
		public TrustedNodeNotifyingBehavior TrustedNodeNotifyingBehavior => P2pNode.Behaviors.Find<TrustedNodeNotifyingBehavior>();
		public Node P2pNode { get; private set; }
		public BlockNotifier BlockNotifier { get; private set; }

		public static async Task<CoreNode> CreateAsync(CoreNodeParams coreNodeParams)
		{
			Guard.NotNull(nameof(coreNodeParams), coreNodeParams);
			using (BenchmarkLogger.Measure())
			{
				var coreNode = new CoreNode();
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

				var configDic = coreNode.Config.ToDictionary();
				string rpcUser = null;
				string rpcPassword = null;
				EndPoint whitebind = null;
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

					if (rpcc != null)
					{
						rpcCookieFilePath = rpcc;
					}
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

				string authString;
				bool cookieAuth = rpcCookieFilePath != null;
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

				coreNode.P2pEndPoint = whitebind ?? coreNodeParams.P2pEndPointStrategy.EndPoint;
				rpcHost ??= coreNodeParams.RpcEndPointStrategy.EndPoint.GetHostOrDefault();
				rpcPort ??= coreNodeParams.RpcEndPointStrategy.EndPoint.GetPortOrDefault();
				EndPointParser.TryParse($"{rpcHost}:{rpcPort}", coreNode.Network.RPCPort, out EndPoint rpce);
				coreNode.RpcEndPoint = rpce;

				coreNode.RpcClient = new RPCClient($"{authString}", coreNode.RpcEndPoint.ToString(coreNode.Network.DefaultPort), coreNode.Network);

				if (coreNodeParams.TryRestart)
				{
					await coreNode.TryStopAsync(false).ConfigureAwait(false);
				}

				if (coreNodeParams.TryDeleteDataDir)
				{
					await IoHelpers.DeleteRecursivelyWithMagicDustAsync(coreNode.DataDir).ConfigureAwait(false);
				}

				IoHelpers.EnsureDirectoryExists(coreNode.DataDir);

				var configPrefix = NetworkTranslator.GetConfigPrefix(coreNode.Network);
				var desiredConfigLines = new List<string>()
				{
					$"{configPrefix}.server			= 1",
					$"{configPrefix}.listen			= 1",
					$"{configPrefix}.whitebind		= {coreNode.P2pEndPoint.ToString(coreNode.Network.DefaultPort)}",
					$"{configPrefix}.rpchost		= {coreNode.RpcEndPoint.GetHostOrDefault()}",
					$"{configPrefix}.rpcport		= {coreNode.RpcEndPoint.GetPortOrDefault()}"
				};

				if (!cookieAuth)
				{
					desiredConfigLines.Add($"{configPrefix}.rpcuser		= {coreNode.RpcClient.CredentialString.UserPassword.UserName}");
					desiredConfigLines.Add($"{configPrefix}.rpcpassword	= {coreNode.RpcClient.CredentialString.UserPassword.Password}");
				}

				if (coreNodeParams.TxIndex != null)
				{
					desiredConfigLines.Add($"{configPrefix}.txindex = {coreNodeParams.TxIndex}");
				}

				if (coreNodeParams.Prune != null)
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
					await File.WriteAllTextAsync(configPath, coreNode.Config.ToString());
				}

				// If it isn't already running, then we run it.
				if (await coreNode.RpcClient.TestAsync().ConfigureAwait(false) is null)
				{
					Logger.LogInfo("Bitcoin Core is already running.");
				}
				else
				{
					coreNode.Bridge = new BitcoindRpcProcessBridge(coreNode.RpcClient, coreNode.DataDir, printToConsole: false);
					await coreNode.Bridge.StartAsync().ConfigureAwait(false);
					Logger.LogInfo("Started Bitcoin Core.");
				}

				using var handshakeTimeout = new CancellationTokenSource();
				handshakeTimeout.CancelAfter(TimeSpan.FromSeconds(21));
				var nodeConnectionParameters = new NodeConnectionParameters()
				{
					UserAgent = $"/WasabiClient:{Constants.ClientVersion.ToString()}/",
					ConnectCancellation = handshakeTimeout.Token,
					IsRelay = true
				};

				nodeConnectionParameters.TemplateBehaviors.Add(new TrustedNodeNotifyingBehavior(coreNode.MempoolService));
				coreNode.P2pNode = await Node.ConnectAsync(coreNode.Network, coreNode.P2pEndPoint, nodeConnectionParameters).ConfigureAwait(false);
				coreNode.P2pNode.VersionHandshake();
				coreNode.P2pNode.StateChanged += coreNode.P2pNode_StateChanged;
				coreNode.P2pNodeStateChangedSubscribed = true;

				coreNode.BlockNotifier = new BlockNotifier(TimeSpan.FromSeconds(7), coreNode.RpcClient, coreNode.TrustedNodeNotifyingBehavior);
				coreNode.BlockNotifier.Start();

				return coreNode;
			}
		}

		private bool P2pNodeStateChangedSubscribed { get; set; }

		private void P2pNode_StateChanged(Node node, NodeState oldState)
		{
			if (node.IsConnected)
			{
				Logger.LogInfo("Local node got connected. Turned on trusted mempool mode.");
				MempoolService.TrustedNodeMode = true;
			}
			else
			{
				Logger.LogInfo("Local node isn't connected. Turned off trusted mempool mode.");
				MempoolService.TrustedNodeMode = false;
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

		private volatile bool _disposedValue = false; // To detect redundant calls

		public async Task DisposeAsync()
		{
			if (!_disposedValue)
			{
				if (BlockNotifier != null)
				{
					await BlockNotifier.StopAsync().ConfigureAwait(false);
				}

				if (P2pNodeStateChangedSubscribed)
				{
					P2pNode.StateChanged -= P2pNode_StateChanged;
				}

				if (P2pNode != null)
				{
					try
					{
						P2pNode?.Disconnect();
					}
					catch (Exception ex)
					{
						Logger.LogDebug(ex);
					}
					finally
					{
						try
						{
							P2pNode?.Dispose();
						}
						catch (Exception ex)
						{
							Logger.LogDebug(ex);
						}
						finally
						{
							P2pNode = null;
							Logger.LogInfo("P2p Bitcoin node is disconnected.");
						}
					}
				}
				_disposedValue = true;
			}
		}

		/// <param name="onlyOwned">Only stop if this node owns the process.</param>
		public async Task<bool> TryStopAsync(bool onlyOwned = true)
		{
			await DisposeAsync().ConfigureAwait(false);

			Exception exThrown = null;

			BitcoindRpcProcessBridge bridge = null;
			if (Bridge != null)
			{
				bridge = Bridge;
			}
			else if (!onlyOwned)
			{
				bridge = new BitcoindRpcProcessBridge(RpcClient, DataDir, printToConsole: false);
			}

			if (bridge != null)
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
