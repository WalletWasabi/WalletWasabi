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
using System.Threading.Tasks;

namespace WalletWasabi.Tests.NodeBuilding
{
	public class CoreNode
	{
		private NodeBuilder Builder { get; }
		public string Folder { get; }

		public int P2pPort { get; }
		public int RpcPort { get; }

		public EndPoint P2pEndPoint { get; }
		public EndPoint RpcEndPoint { get; }

		public string Config { get; }

		public NodeConfigParameters ConfigParameters { get; } = new NodeConfigParameters();

		private CoreNode(string folder, NodeBuilder builder)
		{
			Builder = builder;
			Folder = folder;
			State = CoreNodeState.Stopped;
			DataDir = Path.Combine(folder, "data");
			Directory.CreateDirectory(DataDir);
			var pass = Encoders.Hex.EncodeData(RandomUtils.GetBytes(20));
			Creds = new NetworkCredential(pass, pass);
			Config = Path.Combine(DataDir, "bitcoin.conf");
			ConfigParameters.Import(builder.ConfigParameters);

			var portArray = new int[2];
			var i = 0;
			while (i < portArray.Length)
			{
				var port = RandomUtils.GetUInt32() % 4000;
				port += 10000;
				if (portArray.Any(p => p == port))
				{
					continue;
				}

				try
				{
					var listener = new TcpListener(IPAddress.Loopback, (int)port);
					listener.Start();
					listener.Stop();
					portArray[i] = (int)port;
					i++;
				}
				catch (SocketException)
				{
				}
			}

			P2pPort = portArray[0];
			RpcPort = portArray[1];
			P2pEndPoint = new IPEndPoint(IPAddress.Loopback, P2pPort);
			RpcEndPoint = new IPEndPoint(IPAddress.Loopback, RpcPort);
		}

		public Block[] Generate(int blockCount)
		{
			var rpc = CreateRpcClient();
			var blocks = rpc.Generate(blockCount);
			rpc = rpc.PrepareBatch();
			var tasks = blocks.Select(b => rpc.GetBlockAsync(b)).ToArray();
			rpc.SendBatch();
			return tasks.Select(b => b.GetAwaiter().GetResult()).ToArray();
		}

		public static async Task<CoreNode> CreateAsync(string folder, NodeBuilder builder)
		{
			await IoHelpers.DeleteRecursivelyWithMagicDustAsync(folder);
			Directory.CreateDirectory(folder);

			return new CoreNode(folder, builder);
		}

		public CoreNodeState State { get; private set; }

		internal readonly NetworkCredential Creds;

		public RPCClient CreateRpcClient()
		{
			return new RPCClient($"{Creds.UserName}:{Creds.Password}", RpcEndPoint.ToString(RpcPort), Network.RegTest);
		}

		public async Task<Node> CreateNodeClientAsync()
		{
			return await Node.ConnectAsync(Network.RegTest, P2pEndPoint);
		}

		public async Task<Node> CreateNodeClientAsync(NodeConnectionParameters parameters)
		{
			return await Node.ConnectAsync(Network.RegTest, P2pEndPoint, parameters);
		}

		public async Task StartAsync()
		{
			var config = new NodeConfigParameters
			{
				{"regtest", "1"},
				{"regtest.rest", "1"},
				{"regtest.listenonion", "0"},
				{"regtest.server", "1"},
				{"regtest.txindex", "1"},
				{"regtest.rpcuser", Creds.UserName},
				{"regtest.rpcpassword", Creds.Password},
				{"regtest.whitebind", "127.0.0.1:" + P2pPort.ToString()},
				{"regtest.rpcport", RpcPort.ToString()},
				{"regtest.printtoconsole", "0"}, // Set it to one if do not mind loud debug logs
				{"regtest.keypool", "10"},
				{"regtest.pid", "bitcoind.pid"}
			};
			config.Import(ConfigParameters);
			File.WriteAllText(Config, config.ToString());
			using (await KillerLock.LockAsync())
			{
				Process = Process.Start(new FileInfo(Builder.BitcoinD).FullName, "-conf=bitcoin.conf" + " -datadir=" + DataDir + " -debug=1");
				State = CoreNodeState.Starting;
				string pidFile = Path.Combine(DataDir, "regtest", "bitcoind.pid");
				if (!File.Exists(pidFile))
				{
					Directory.CreateDirectory(Path.Combine(DataDir, "regtest"));
					File.WriteAllText(pidFile, Process.Id.ToString());
				}
			}
			while (true)
			{
				try
				{
					await CreateRpcClient().GetBlockHashAsync(0);
					State = CoreNodeState.Running;
					break;
				}
				catch
				{
				}
				if (Process is null || Process.HasExited)
				{
					break;
				}
			}
		}

		private Process Process { get; set; }
		private string DataDir { get; }

		private readonly AsyncLock KillerLock = new AsyncLock();

		public async Task TryKillAsync(bool cleanFolder = true)
		{
			try
			{
				using (await KillerLock.LockAsync())
				{
					try
					{
						await CreateRpcClient().StopAsync();
						if (!Process.WaitForExit(20000))
						{
							//log this
						}
					}
					catch (Exception)
					{ }

					State = CoreNodeState.Killed;
				}
				if (cleanFolder)
				{
					await IoHelpers.DeleteRecursivelyWithMagicDustAsync(Folder);
				}
			}
			catch
			{ }
		}

		public async Task BroadcastBlocksAsync(IEnumerable<Block> blocks)
		{
			using (var node = await CreateNodeClientAsync())
			{
				node.VersionHandshake();
				BroadcastBlocks(blocks, node);
			}
		}

		public void BroadcastBlocks(IEnumerable<Block> blocks, Node node)
		{
			foreach (var block in blocks)
			{
				node.SendMessageAsync(new InvPayload(block));
				node.SendMessageAsync(new BlockPayload(block));
			}
			node.PingPong();
		}
	}
}
