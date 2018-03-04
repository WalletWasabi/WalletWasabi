using NBitcoin;
using NBitcoin.Crypto;
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MagicalCryptoWallet.Tests.NodeBuilding
{
	public class CoreNode
	{
		private readonly NodeBuilder _Builder;
		private string _folder;
		public string Folder
		{
			get
			{
				return _folder;
			}
		}

		public IPEndPoint Endpoint
		{
			get
			{
				return new IPEndPoint(IPAddress.Parse("127.0.0.1"), _ports[0]);
			}
		}

		public string Config
		{
			get
			{
				return _config;
			}
		}

		private readonly NodeConfigParameters _ConfigParameters = new NodeConfigParameters();
		private string _config;

		public NodeConfigParameters ConfigParameters
		{
			get
			{
				return _ConfigParameters;
			}
		}

		public CoreNode(string folder, NodeBuilder builder)
		{
			_Builder = builder;
			_folder = folder;
			_state = CoreNodeState.Stopped;
			CleanFolder();
			Directory.CreateDirectory(folder);
			DataDir = Path.Combine(folder, "data");
			Directory.CreateDirectory(DataDir);
			var pass = Encoders.Hex.EncodeData(RandomUtils.GetBytes(20));
			Creds = new NetworkCredential(pass, pass);
			_config = Path.Combine(DataDir, "bitcoin.conf");
			ConfigParameters.Import(builder.ConfigParameters);
			_ports = new int[2];
			FindPorts(_ports);
		}

		private void CleanFolder()
		{
			try
			{
				IoHelpers.DeleteRecursivelyWithMagicDust(_folder);
			}
			catch (DirectoryNotFoundException) { }
		}
#if !NOSOCKET
		public void Sync(CoreNode node, bool keepConnection = false)
		{
			var rpc = CreateRPCClient();
			var rpc1 = node.CreateRPCClient();
			rpc.AddNode(node.Endpoint, true);
			while (rpc.GetBestBlockHash() != rpc1.GetBestBlockHash())
			{
				Thread.Sleep(200);
			}
			if (!keepConnection)
				rpc.RemoveNode(node.Endpoint);
		}
#endif
		private CoreNodeState _state;
		public CoreNodeState State
		{
			get
			{
				return _state;
			}
		}

		private int[] _ports;

		public int ProtocolPort
		{
			get
			{
				return _ports[0];
			}
		}
		public void Start()
		{
			StartAsync().GetAwaiter().GetResult();
		}

		private readonly NetworkCredential Creds;
		public RPCClient CreateRPCClient()
		{
			return new RPCClient(Creds, new Uri("http://127.0.0.1:" + _ports[1] + "/"), Network.RegTest);
		}

		public RestClient CreateRESTClient()
		{
			return new RestClient(new Uri("http://127.0.0.1:" + _ports[1] + "/"));
		}
#if !NOSOCKET
		public Node CreateNodeClient()
		{
			return Node.Connect(Network.RegTest, "127.0.0.1:" + _ports[0]);
		}
		public Node CreateNodeClient(NodeConnectionParameters parameters)
		{
			return Node.Connect(Network.RegTest, "127.0.0.1:" + _ports[0], parameters);
		}
#endif

		public async Task StartAsync()
		{
			NodeConfigParameters config = new NodeConfigParameters
			{
				{"regtest", "1"},
				{"rest", "1"},
				{"server", "1"},
				{"txindex", "0"},
				{"rpcuser", Creds.UserName},
				{"rpcpassword", Creds.Password},
				{"whitebind", "127.0.0.1:" + _ports[0].ToString()},
				{"rpcport", _ports[1].ToString()},
				{"printtoconsole", "1"},
				{"keypool", "10"}
			};
			config.Import(ConfigParameters);
			File.WriteAllText(_config, config.ToString());
			lock (_l)
			{
				_process = Process.Start(new FileInfo(_Builder.BitcoinD).FullName, "-conf=bitcoin.conf" + " -datadir=" + DataDir + " -debug=net");
				_state = CoreNodeState.Starting;
			}
			while (true)
			{
				try
				{
					await CreateRPCClient().GetBlockHashAsync(0).ConfigureAwait(false);
					_state = CoreNodeState.Running;
					break;
				}
				catch { }
				if (_process == null || _process.HasExited)
					break;
			}
		}


		private Process _process;
		private readonly string DataDir;

		private void FindPorts(int[] portArray)
		{
			int i = 0;
			while (i < portArray.Length)
			{
				var port = RandomUtils.GetUInt32() % 4000;
				port = port + 10000;
				if (portArray.Any(p => p == port))
					continue;
				try
				{
					TcpListener listener = new TcpListener(IPAddress.Loopback, (int)port);
					listener.Start();
					listener.Stop();
					portArray[i] = (int)port;
					i++;
				}
				catch (SocketException) { }
			}
		}

		private List<Transaction> _transactions = new List<Transaction>();
		private HashSet<OutPoint> _locked = new HashSet<OutPoint>();
		private Money _fee = Money.Coins(0.0001m);
		public Transaction GiveMoney(Script destination, Money amount, bool broadcast = true)
		{
			var rpc = CreateRPCClient();
			TransactionBuilder builder = new TransactionBuilder();
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

#if !NOSOCKET
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
#else
        public void Broadcast(Transaction transaction)
        {
            var rpc = CreateRPCClient();
            rpc.SendRawTransaction(transaction);
        }
#endif
		public void SelectMempoolTransactions()
		{
			var rpc = CreateRPCClient();
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
			var rpc = CreateRPCClient();
			TransactionBuilder builder = new TransactionBuilder();
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

		private object _l = new object();
		public void Kill(bool cleanFolder = true)
		{
			lock (_l)
			{
				if (_process != null && !_process.HasExited)
				{
					_process.Kill();
					_process.WaitForExit();
				}
				_state = CoreNodeState.Killed;
				if (cleanFolder)
					CleanFolder();
			}
		}

		public DateTimeOffset? MockTime
		{
			get;
			set;
		}

		public void SetMinerSecret(BitcoinSecret secret)
		{
			CreateRPCClient().ImportPrivKey(secret);
			MinerSecret = secret;
		}

		public BitcoinSecret MinerSecret
		{
			get;
			private set;
		}

		public Block[] Generate(int blockCount, bool includeUnbroadcasted = true, bool broadcast = true)
		{
			var rpc = CreateRPCClient();
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

		private Transaction DoMalleate(Transaction transaction)
		{
			transaction = transaction.Clone();
			if (!transaction.IsCoinBase)
				foreach (var input in transaction.Inputs)
				{
					List<Op> malleated = new List<Op>();
					foreach (var op in input.ScriptSig.ToOps())
					{
						try
						{
							var sig = new TransactionSignature(op.PushData);
							sig = MakeHighS(sig);
							malleated.Add(Op.GetPushOp(sig.ToBytes()));
						}
						catch { malleated.Add(op); }
					}
					input.ScriptSig = new Script(malleated.ToArray());
				}
			return transaction;
		}



		private TransactionSignature MakeHighS(TransactionSignature sig)
		{
			var curveOrder = new NBitcoin.BouncyCastle.Math.BigInteger("115792089237316195423570985008687907852837564279074904382605163141518161494337", 10);
			var ecdsa = new ECDSASignature(sig.Signature.R, sig.Signature.S.Negate().Mod(curveOrder));
			return new TransactionSignature(ecdsa, sig.SigHash);
		}

		public void BroadcastBlocks(Block[] blocks)
		{
			using (var node = CreateNodeClient())
			{
				node.VersionHandshake();
				BroadcastBlocks(blocks, node);
			}
		}

		public void BroadcastBlocks(Block[] blocks, Node node)
		{
			foreach (var block in blocks)
			{
				node.SendMessageAsync(new InvPayload(block));
				node.SendMessageAsync(new BlockPayload(block));
			}
			node.PingPong();
		}

		public Block[] FindBlock(int blockCount = 1, bool includeMempool = true)
		{
			SelectMempoolTransactions();
			return Generate(blockCount, includeMempool);
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

		private List<Transaction> Reorder(List<Transaction> txs)
		{
			if (txs.Count == 0)
				return txs;
			var result = new List<Transaction>();
			var dictionary = txs.ToDictionary(t => t.GetHash(), t => new TransactionNode(t));
			foreach (var transaction in dictionary.Select(d => d.Value))
			{
				foreach (var input in transaction.Transaction.Inputs)
				{
					var node = dictionary.TryGet(input.PrevOut.Hash);
					if (node != null)
					{
						transaction.DependsOn.Add(node);
					}
				}
			}
			while (dictionary.Count != 0)
			{
				foreach (var node in dictionary.Select(d => d.Value).ToList())
				{
					foreach (var parent in node.DependsOn.ToList())
					{
						if (!dictionary.ContainsKey(parent.Hash))
							node.DependsOn.Remove(parent);
					}
					if (node.DependsOn.Count == 0)
					{
						result.Add(node.Transaction);
						dictionary.Remove(node.Hash);
					}
				}
			}
			return result;
		}

		private BitcoinSecret GetFirstSecret(RPCClient rpc)
		{
			if (MinerSecret != null)
				return MinerSecret;
			var dest = rpc.ListSecrets().FirstOrDefault();
			if (dest == null)
			{
				var address = rpc.GetNewAddress();
				dest = rpc.DumpPrivKey(address);
			}
			return dest;
		}
	}
}
