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

		public IPEndPoint Endpoint
		{
			get
			{
				return new IPEndPoint(IPAddress.Parse("127.0.0.1"), _ports[0]);
			}
		}

		public string Config { get; }

		public NodeConfigParameters ConfigParameters { get; } = new NodeConfigParameters();

		public CoreNode(string folder, NodeBuilder builder)
		{
			_Builder = builder;
			Folder = folder;
			State = CoreNodeState.Stopped;
			CleanFolderAsync().GetAwaiter().GetResult();
			Directory.CreateDirectory(folder);
			DataDir = Path.Combine(folder, "data");
			Directory.CreateDirectory(DataDir);
			var pass = Encoders.Hex.EncodeData(RandomUtils.GetBytes(20));
			Creds = new NetworkCredential(pass, pass);
			Config = Path.Combine(DataDir, "bitcoin.conf");
			ConfigParameters.Import(builder.ConfigParameters);
			_ports = new int[2];
			FindPorts(_ports);
		}

		private async Task CleanFolderAsync()
		{
			await IoHelpers.DeleteRecursivelyWithMagicDustAsync(Folder);
		}

		public async Task SyncAsync(CoreNode node, bool keepConnection = false)
		{
			var rpc = CreateRpcClient();
			var rpc1 = node.CreateRpcClient();
			rpc.AddNode(node.Endpoint, true);
			while (await rpc.GetBestBlockHashAsync() != await rpc1.GetBestBlockHashAsync())
			{
				await Task.Delay(200);
			}
			if (!keepConnection)
			{
				rpc.RemoveNode(node.Endpoint);
			}
		}

		public CoreNodeState State { get; private set; }

		private readonly int[] _ports;

		public int ProtocolPort
		{
			get
			{
				return _ports[0];
			}
		}

		private readonly NetworkCredential Creds;

		public RPCClient CreateRpcClient()
		{
			return new RPCClient(Creds, new Uri("http://127.0.0.1:" + _ports[1] + "/"), Network.RegTest);
		}

		public RestClient CreateRESTClient()
		{
			return new RestClient(new Uri("http://127.0.0.1:" + _ports[1] + "/"));
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

		private List<Transaction> _transactions = new List<Transaction>();
		private HashSet<OutPoint> _locked = new HashSet<OutPoint>();
		private readonly Money _fee = Money.Coins(0.0001m);

		public Transaction GiveMoney(Script destination, Money amount, bool broadcast = true)
		{
			var rpc = CreateRpcClient();
			var builder = new TransactionBuilder();
			builder.AddKeys(rpc.ListSecrets().OfType<ISecret>().ToArray());
			builder.AddCoins(rpc.ListUnspent().Where(c => !_locked.Contains(c.OutPoint)).Select(c => c.AsCoin()));
			builder.Send(destination, amount);
			builder.SendFees(_fee);
			builder.SetChange(GetFirstSecret(rpc));
			var tx = builder.BuildTransaction(true);
			foreach (var outpoint in tx.Inputs.Select(i => i.PrevOut))
			{
				_locked.Add(outpoint);
			}
			if (broadcast)
				Broadcast(tx);
			else
				_transactions.Add(tx);
			return tx;
		}

		public void Rollback(Transaction tx)
		{
			_transactions.Remove(tx);
			foreach (var outpoint in tx.Inputs.Select(i => i.PrevOut))
			{
				_locked.Remove(outpoint);
			}
		}

		public void Broadcast(Transaction transaction)
		{
			using (var node = CreateNodeClient())
			{
				node.VersionHandshake();
				node.SendMessageAsync(new InvPayload(transaction));
				node.SendMessageAsync(new TxPayload(transaction));
				node.PingPong();
			}
		}

		public void SelectMempoolTransactions()
		{
			var rpc = CreateRpcClient();
			var txs = rpc.GetRawMempool();

			var tasks = txs.Select(t => rpc.GetRawTransactionAsync(t)).ToArray();
			Task.WaitAll(tasks);
			_transactions.AddRange(tasks.Select(t => t.GetAwaiter().GetResult()).ToArray());
		}

		public void Broadcast(Transaction[] txs)
		{
			foreach (var tx in txs)
				Broadcast(tx);
		}

		public void Split(Money amount, int parts)
		{
			var rpc = CreateRpcClient();
			var builder = new TransactionBuilder();
			builder.AddKeys(rpc.ListSecrets().OfType<ISecret>().ToArray());
			builder.AddCoins(rpc.ListUnspent().Select(c => c.AsCoin()));
			var secret = GetFirstSecret(rpc);
			foreach (var part in (amount - _fee).Split(parts))
			{
				builder.Send(secret, part);
			}
			builder.SendFees(_fee);
			builder.SetChange(secret);
			var tx = builder.BuildTransaction(true);
			Broadcast(tx);
		}

		private readonly object _l = new object();

		public void Kill(bool cleanFolder = true)
		{
			lock (_l)
			{
				if (!(_process is null) && !_process.HasExited)
				{
					_process.Kill();
					_process.WaitForExit();
				}
				State = CoreNodeState.Killed;
				if (cleanFolder)
					CleanFolderAsync().GetAwaiter().GetResult();
			}
		}

		public DateTimeOffset? MockTime
		{
			get;
			set;
		}

		public void SetMinerSecret(BitcoinSecret secret)
		{
			CreateRpcClient().ImportPrivKey(secret);
			MinerSecret = secret;
		}

		public BitcoinSecret MinerSecret
		{
			get;
			private set;
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

		private List<uint256> _toMalleate = new List<uint256>();

		public void Malleate(uint256 txId)
		{
			_toMalleate.Add(txId);
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

		public void MineBlock(Block block)
		{
			block.UpdateMerkleRoot();
			uint nonce = 0;
			while (!block.CheckProofOfWork())
			{
				block.Header.Nonce = ++nonce;
			}
		}

		private class TransactionNode
		{
			public TransactionNode(Transaction tx)
			{
				Transaction = tx;
				Hash = tx.GetHash();
			}

			public uint256 Hash = null;
			public Transaction Transaction = null;
			public List<TransactionNode> DependsOn = new List<TransactionNode>();
		}

		private BitcoinSecret GetFirstSecret(RPCClient rpc)
		{
			if (!(MinerSecret is null))
				return MinerSecret;
			var dest = rpc.ListSecrets().FirstOrDefault();
			if (dest is null)
			{
				var address = rpc.GetNewAddress();
				dest = rpc.DumpPrivKey(address);
			}
			return dest;
		}
	}
}
