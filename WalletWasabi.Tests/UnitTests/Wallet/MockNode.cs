using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;

namespace WalletWasabi.Tests.UnitTests.Wallet;

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

	public static async Task<MockNode> CreateNodeAsync()
	{
		var node = new MockNode();
		await node.Wallet.GenerateAsync(101, CancellationToken.None).ConfigureAwait(false);
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
		var header = uint256.Zero;
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
			var entries = scripts.Select(x => x.ToBytes()).DefaultIfEmpty(IndexBuilderService.DummyScript[0]);

			var filter = new GolombRiceFilterBuilder()
				.SetKey(block.GetHash())
				.AddEntries(entries)
				.Build();

			var tipFilter = filters.Last();

			header = filter.GetHeader(header);
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

	public async Task GenerateBlockAsync(CancellationToken cancel) =>
		await Rpc.GenerateToAddressAsync(1, Wallet.GetNextDestination().ScriptPubKey.GetDestinationAddress(Network)!, cancel).ConfigureAwait(false);
}
