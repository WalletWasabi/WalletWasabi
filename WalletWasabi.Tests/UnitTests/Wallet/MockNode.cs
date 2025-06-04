using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.RPC;
using WalletWasabi.Indexer.Models;
using WalletWasabi.BitcoinRpc.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;

namespace WalletWasabi.Tests.UnitTests.Wallet;

public record MockNodeOptions(int BlockToGenerate);
public class MockNode
{
	private MockNode()
	{
		Rpc = new MockRpcClient();
		BlockChain = new Dictionary<uint256, Block>();
		Mempool = new Dictionary<uint256, Transaction>();
		Wallet = new TestWallet("MinerWallet", Rpc);

		Rpc.OnGenerateToAddressAsync = (blockCount, address) => Task.FromResult(
			Enumerable
				.Range(0, blockCount)
				.Select(_ => CreateBlock(address))
				.Select(block => block.GetHash())
				.ToArray());

		Rpc.OnGetBlockAsync = (blockHash) => Task.FromResult(BlockChain[blockHash]);
		Rpc.OnGetBlockHashAsync = (height) => Task.FromResult(BlockChain.Keys.ElementAt(height));
		Rpc.OnGetVerboseBlockAsync = (blockHash) =>
		{
			var block = BlockChain[blockHash];
			var height = BlockChain.TakeWhile(x => x.Key != blockHash).Count();
			var blockInfo = new VerboseBlockInfo(
				block.Header.HashPrevBlock,
				(uint)height,
				blockHash,
				DateTimeOffset.UtcNow.AddMinutes(height * 10),
				(uint)height,
				[]);

			return Task.FromResult(blockInfo);
		};
		Rpc.OnGetBestBlockHashAsync = () =>
			Task.FromResult(BlockChain.Count == 0 ? Network.RegTest.GenesisHash : BlockChain.Last().Key);
		Rpc.OnGetBlockchainInfoAsync = () => Task.FromResult(new BlockchainInfo
		{
			Headers = (uint) BlockChain.Count - 1,
			Blocks = (uint) BlockChain.Count - 1,
			BestBlockHash = BlockChain.Count == 1 ? Network.RegTest.GenesisHash : BlockChain.Last().Key,
			InitialBlockDownload = false
		});

		Rpc.OnGetRawTransactionAsync = (txHash, _) => Task.FromResult(
			BlockChain.Values
				.SelectMany(block => block.Transactions)
				.First(tx => tx.GetHash() == txHash));

		Rpc.OnSendRawTransactionAsync = (tx) =>
		{
			var txId = tx.GetHash();
			Mempool.Add(txId, tx);
			return txId;
		};
	}

	public Network Network => Network.RegTest;
	public MockRpcClient Rpc { get; }
	public Dictionary<uint256, Block> BlockChain { get; }
	public Dictionary<uint256, Transaction> Mempool { get; }
	public TestWallet Wallet { get; }

	public static async Task<MockNode> CreateNodeAsync(MockNodeOptions? options = null)
	{
		options ??= new MockNodeOptions(101);
		var node = new MockNode();
		node.BlockChain[Network.RegTest.GenesisHash] = Network.RegTest.GetGenesis();
		await node.Wallet.GenerateAsync(options.BlockToGenerate, CancellationToken.None).ConfigureAwait(false);
		return node;
	}

	public IEnumerable<FilterModel> BuildFilters()
	{
		Dictionary<OutPoint, Script> outPoints = BlockChain.Values
			.SelectMany(block => block.Transactions)
			.SelectMany(tx => tx.Outputs.AsIndexedOutputs())
			.ToDictionary(output => new OutPoint(output.Transaction, output.N), output => output.TxOut.ScriptPubKey);

		List<FilterModel> filters = new();

		var startingFilter = StartingFilters.GetStartingFilter(Network);
		filters.Add(startingFilter);
		foreach (var block in BlockChain.Values)
		{
			var inputScriptPubKeys = block.Transactions
				.SelectMany(tx => tx.Inputs)
				.Where(input => outPoints.ContainsKey(input.PrevOut))
				.Select(input => outPoints[input.PrevOut]);

			var outputScriptPubKeys = block.Transactions
				.SelectMany(tx => tx.Outputs)
				.Select(output => output.ScriptPubKey);

			var scripts = inputScriptPubKeys.Union(outputScriptPubKeys);
			var entries = scripts.Select(x => x.ToBytes()).DefaultIfEmpty(LegacyWasabiFilterGenerator.DummyScript[0]);

			var filter = new GolombRiceFilterBuilder()
				.SetKey(block.GetHash())
				.AddEntries(entries)
				.Build();

			var tipFilter = filters.Last();
			var header = filter.GetHeader(tipFilter.Header.HeaderOrPrevBlockHash);
			var smartHeader = new SmartHeader(block.GetHash(), header, tipFilter.Header.Height + 1, DateTimeOffset.UtcNow);
			filters.Add(new FilterModel(smartHeader, filter));
		}

		return filters;
	}

	private Block CreateBlock(BitcoinAddress address)
	{
		Block block = Network.Consensus.ConsensusFactory.CreateBlock();
		block.Header.HashPrevBlock = BlockChain.Keys.LastOrDefault() ?? uint256.Zero;
		var coinBaseTransaction = Transaction.Create(Network);

		var amount = Money.Coins(5) + Money.Satoshis(BlockChain.Count); // Add block height to make sure the coinbase tx hash differs.
		coinBaseTransaction.Outputs.Add(amount, address);
		block.AddTransaction(coinBaseTransaction);

		foreach (var tx in Mempool.Values)
		{
			block.AddTransaction(tx);
		}
		Mempool.Clear();
		BlockChain.Add(block.GetHash(), block);
		return block;
	}

	public async Task<uint256[]> GenerateBlockAsync(CancellationToken cancel) =>
		await Rpc.GenerateToAddressAsync(1, Wallet.GetNextDestination().ScriptPubKey.GetDestinationAddress(Network)!, cancel).ConfigureAwait(false);
}
