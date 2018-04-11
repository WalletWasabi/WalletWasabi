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
using System.Threading;
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
			try
			{
				await IoHelpers.DeleteRecursivelyWithMagicDustAsync(Folder);
			}
			catch (DirectoryNotFoundException) { }
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

        private int[] _ports;

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
			return Node.Connect(Network.RegTest, new IPEndPoint(IPAddress.Loopback,_ports[0]));
		}
		public Node CreateNodeClient(NodeConnectionParameters parameters)
		{
			return Node.Connect(Network.RegTest, new IPEndPoint(IPAddress.Loopback,_ports[0]), parameters);
		}

		public async Task StartAsync()
		{
			NodeConfigParameters config = new NodeConfigParameters
			{
				{"regtest", "1"},
				{"rest", "1"},
				{"listenonion", "0"},
				{"server", "1"},
				{"txindex", "0"},
				{"rpcuser", Creds.UserName},
				{"rpcpassword", Creds.Password},
				{"whitebind", "127.0.0.1:" + _ports[0].ToString()},
				{"rpcport", _ports[1].ToString()},
				{"printtoconsole", "0"}, // Set it to one if don't mind loud debug logs
				{"keypool", "10"}
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
					await CreateRpcClient().GetBlockHashAsync(0).ConfigureAwait(false);
					State = CoreNodeState.Running;
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
			var rpc = CreateRpcClient();
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
				State = CoreNodeState.Killed;
				if (cleanFolder)
					CleanFolderAsync().GetAwaiter().GetResult(); ;
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

		public Block[] Generate(int blockCount, bool includeUnbroadcasted = true, bool broadcast = true)
		{
			var rpc = CreateRpcClient();
			var blocks = rpc.Generate(blockCount);
			rpc = rpc.PrepareBatch();
			var tasks = blocks.Select(b => rpc.GetBlockAsync(b)).ToArray();
			rpc.SendBatch();
			return tasks.Select(b => b.GetAwaiter().GetResult()).ToArray();
		}

		public async Task<Block[]> GenerateEmptyBlocksAsync(int height, BitcoinAddress minerAddress, int blockCount)
		{
			var rpc = CreateRpcClient();
			var bestBlock = await rpc.GetBlockAsync(height);
			ConcurrentChain chain = null;
			var blocks = new List<Block>();
			var now = MockTime == null ? DateTimeOffset.UtcNow : MockTime.Value;
			using(var node = CreateNodeClient())
			{
				node.VersionHandshake();
				chain = node.GetChain();
				for(var i = 0; i < blockCount; i++)
				{
					uint nonce = 0;
					var block = rpc.Network.Consensus.ConsensusFactory.CreateBlock();
					block.Header.HashPrevBlock = chain.Tip.HashBlock;
					block.Header.Bits = block.Header.GetWorkRequired(rpc.Network, chain.Tip);
					block.Header.UpdateTime(now, rpc.Network, chain.Tip);
					var coinbase = new Transaction();
					coinbase.AddInput(TxIn.CreateCoinbase(chain.Height + 1));
					coinbase.AddOutput(new TxOut(rpc.Network.GetReward(chain.Height + 1), minerAddress));
					block.AddTransaction(coinbase);
					block.UpdateMerkleRoot();
					while(!block.CheckProofOfWork())
						block.Header.Nonce = ++nonce;
					blocks.Add(block);
					chain.SetTip(block.Header);
				}
				BroadcastBlocks(blocks.ToArray(), node);
			}
			return blocks.ToArray();
		}

		public async Task<Block[]> GenerateEmptyBlockAsync(int height, BitcoinAddress minerAddress, int blockCount)
		{
			var now = DateTimeOffset.UtcNow;
			var rpc = CreateRpcClient();
			var curBlock = await rpc.GetBlockAsync(height);
			
			var blocks = new List<Block>(blockCount);
			
			for (var i = 0; i < blockCount; i++)
			{
				var prevBlock = curBlock;
				var blockTime = now + TimeSpan.FromMinutes(i + 1);
				curBlock = curBlock.CreateNextBlockWithCoinbase(minerAddress, height + i + 1, blockTime);
				curBlock.Header.Bits = curBlock.Header.GetWorkRequired(rpc.Network, new ChainedBlock(prevBlock.Header, height + i + 1));

				MineBlock(curBlock);
				blocks.Add(curBlock);
			}

			BroadcastBlocks(blocks);
			return blocks.ToArray();
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
		
		public Block[] FindBlock(int blockCount = 1, bool includeMempool = true)
		{
			SelectMempoolTransactions();
			return Generate(blockCount, includeMempool);
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
