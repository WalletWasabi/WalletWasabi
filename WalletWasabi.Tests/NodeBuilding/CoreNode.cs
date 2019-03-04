using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using NBitcoin.RPC;
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
		private readonly NodeBuilder _Builder;
		public string Folder { get; }

		public IPEndPoint Endpoint => new IPEndPoint(IPAddress.Parse("127.0.0.1"), _ports[0]);

		public string Config { get; }

		public NodeConfigParameters ConfigParameters { get; } = new NodeConfigParameters();

		private CoreNode(string folder, NodeBuilder builder)
		{
			_Builder = builder;
			Folder = folder;
			State = CoreNodeState.Stopped;
			DataDir = Path.Combine(folder, "data");
			Directory.CreateDirectory(DataDir);
			var pass = Encoders.Hex.EncodeData(RandomUtils.GetBytes(20));
			Creds = new NetworkCredential(pass, pass);
			Config = Path.Combine(DataDir, "bitcoin.conf");
			ConfigParameters.Import(builder.ConfigParameters);
			_ports = new int[2];
			FindPorts(_ports);
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

		private readonly int[] _ports;

		private readonly NetworkCredential Creds;

		public RPCClient CreateRpcClient()
		{
			return new RPCClient(Creds, new Uri("http://127.0.0.1:" + _ports[1] + "/"), Network.RegTest);
		}

		public Node CreateNodeClient()
		{
			return Node.Connect(Network.RegTest, new IPEndPoint(IPAddress.Loopback, _ports[0]));
		}

		public Node CreateNodeClient(NodeConnectionParameters parameters)
		{
			return Node.Connect(Network.RegTest, new IPEndPoint(IPAddress.Loopback, _ports[0]), parameters);
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
				{"regtest.whitebind", "127.0.0.1:" + _ports[0].ToString()},
				{"regtest.rpcport", _ports[1].ToString()},
				{"regtest.printtoconsole", "0"}, // Set it to one if don't mind loud debug logs
				{"regtest.keypool", "10"}
			};
			config.Import(ConfigParameters);
			File.WriteAllText(Config, config.ToString());
			lock (_l)
			{
				_process = Process.Start(new FileInfo(_Builder.BitcoinD).FullName, "-conf=bitcoin.conf" + " -datadir=" + DataDir + " -debug=1");
				State = CoreNodeState.Starting;
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
				if (_process is null || _process.HasExited)
					break;
			}
		}

		private Process _process;
		private readonly string DataDir;

		private static void FindPorts(int[] portArray)
		{
			var i = 0;
			while (i < portArray.Length)
			{
				var port = RandomUtils.GetUInt32() % 4000;
				port = port + 10000;
				if (portArray.Any(p => p == port))
					continue;
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
		}

		private readonly object _l = new object();

		public void Kill(bool cleanFolder = true)
		{
			lock (_l)
			{
				if (_process != null && !_process.HasExited)
				{
					_process.Kill();
					_process.WaitForExit();
				}
				State = CoreNodeState.Killed;
				if (cleanFolder)
				{
					IoHelpers.DeleteRecursivelyWithMagicDustAsync(Folder).GetAwaiter().GetResult();
				}
			}
		}

		public void BroadcastBlocks(IEnumerable<Block> blocks)
		{
			using (var node = CreateNodeClient())
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
