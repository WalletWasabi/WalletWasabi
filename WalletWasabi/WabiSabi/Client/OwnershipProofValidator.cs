using NBitcoin;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Crypto;
using WalletWasabi.Models;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

// OwnershipProofValidator validates the ownership proofs provided by the coordinator.
public class OwnershipProofValidator
{
	public OwnershipProofValidator(IndexStore indexStore, TransactionStore transactionStore, IBlockProvider blockProvider)
	{
		IndexStore = indexStore;
		TransactionStore = transactionStore;
		BlockProvider = blockProvider;
	}

	private IndexStore IndexStore { get; }
	private TransactionStore TransactionStore { get; }
	private IBlockProvider BlockProvider { get; }

	// Verifies the coins and ownership proofs. These proofs are provided by the coordinator who should
	// have validate them before which means that when dealing with a honest coordinator all these proof
	// are valid.
	//
	// In case one proof is invalid we have evidence enough to know the coordinator is malicious and we
	// have to abort the process, notify the user and ban the coordinator.
	public async ValueTask<int> VerifyOtherAlicesOwnershipProofsAsync(
		CoinJoinInputCommitmentData coinJoinInputCommitmentData,
		ImmutableList<(Coin Coin, OwnershipProof OwnershipProof)> othersCoins,
		int stopAfter,
		CancellationToken cancellationToken)
	{
		var proofChannel = Channel.CreateBounded<(Coin, Coin, OwnershipProof)>(10);
			
		var coinsSearchingTask = Task.Run(() => FindCoinsToVerifyAsync(proofChannel.Writer, othersCoins, cancellationToken), cancellationToken);
		var coinsValidationTask = Task.Run(() => ValidateCoinsAsync(proofChannel.Reader, coinJoinInputCommitmentData, stopAfter, cancellationToken));

		await Task.WhenAll(coinsSearchingTask, coinsValidationTask).ConfigureAwait(false);
		return await coinsValidationTask.ConfigureAwait(false);
	}

	private async Task FindCoinsToVerifyAsync(
		ChannelWriter<(Coin, Coin, OwnershipProof)> proofChannelWriter,
		ImmutableList<(Coin Coin, OwnershipProof OwnershipProof)> othersCoins,
		CancellationToken cancellationToken)
	{
			
		// What follows is an optimization where we try to verify coins that are already known by us.
		var mineTxIds = TransactionStore.GetTransactionHashes();
		var alreadySeenCoins = othersCoins.Where(x => mineTxIds.Contains(x.Coin.Outpoint.Hash));

		// In case one Alice is trying to spend a output from a transaction that we already have
		// seen before then we can verify its script without downloading anything from the network.
		// However it is impossible for us to know it the coins is unspent!.
		foreach (var (coin, ownershipProof) in alreadySeenCoins)
		{
			if (TransactionStore.TryGetTransaction(coin.Outpoint.Hash, out var stx))
			{
				var coinIndex = (int) coin.Outpoint.N; 
				if (coinIndex >= stx.Transaction.Outputs.Count)
				{
					throw new MaliciousCoordinatorException("Fake coin with impossible index.");
				}
				var foundCoin = new Coin(stx.Transaction, stx.Transaction.Outputs[coinIndex]);
				await proofChannelWriter.WriteAsync((coin, foundCoin, ownershipProof), cancellationToken).ConfigureAwait(false);
			}
		}

		// In case there are no filters.
		if (IndexStore.SmartHeaderChain.HashCount == 0)
		{
			proofChannelWriter.Complete();
			return;
		}

		// In case we cannot validate enough proofs using only our already seen transactions, we
		// would need to start downloading the blocks containing the coins that need to be
		// validated. We use our block filters for that.
		var scripts = othersCoins.Select(x => x.Coin.ScriptPubKey.ToCompressedBytes()).ToArray();
			
		// Do not query more than 10% of the blocks
		var maxFiltersToQueryCount = (uint)(IndexStore.SmartHeaderChain.HashCount / 10.0);
		var filters = IndexStore.GetFiltersFromHeightAsync(
			fromHeight: new Height(IndexStore.SmartHeaderChain.TipHeight - maxFiltersToQueryCount),
			cancellationToken).ConfigureAwait(false);

		await foreach (var filterModel in filters)
		{
			var matchFound = filterModel.Filter.MatchAny(scripts, filterModel.FilterKey);
			if (matchFound)
			{
				var blockId = filterModel.Header.BlockHash;
				var block = await BlockProvider.GetBlockAsync(blockId, cancellationToken).ConfigureAwait(false);

				foreach (var (coin, ownershipProof) in othersCoins)
				{
					if (block.Transactions.FirstOrDefault(x => x.GetHash() == coin.Outpoint.Hash) is { } tx)
					{
						var coinIndex = (int) coin.Outpoint.N; 
						if (coinIndex >= tx.Outputs.Count)
						{
							throw new MaliciousCoordinatorException("Fake coin with impossible index.");
						}
						var foundCoin = new Coin(tx, tx.Outputs[coinIndex]);
						await proofChannelWriter.WriteAsync((coin, foundCoin, ownershipProof), cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}
			
		// there are no more filters
		//if (filterModel.Header.BlockHash == IndexStore.SmartHeaderChain.TipHash)
		//{
		proofChannelWriter.Complete();
		//}
	}

	private async Task<int> ValidateCoinsAsync(
		ChannelReader<(Coin, Coin, OwnershipProof)> proofChannelReader,
		CoinJoinInputCommitmentData coinJoinInputCommitmentData,
		int stopAfter,
		CancellationToken cancellationToken)
	{
		// Consumes and validates the coins and their proofs.
		var validProofs = 0;
		while (validProofs < stopAfter && await proofChannelReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var (aliceCoin, realCoin, ownershipProof) = await proofChannelReader.ReadAsync(cancellationToken).ConfigureAwait(false);
			if (realCoin is not { } coin || (aliceCoin.TxOut, aliceCoin.Outpoint) != (coin.TxOut, coin.Outpoint))
			{
				throw new MaliciousCoordinatorException("Coin doesn't exists or is different than the provided by the coordinator.");
			}
			if (!OwnershipProof.VerifyCoinJoinInputProof(ownershipProof, aliceCoin.ScriptPubKey, coinJoinInputCommitmentData))
			{
				throw new MaliciousCoordinatorException("The ownership proof is not valid what means Alice cannot really spend it.");
			}
			validProofs++;
		}

		return validProofs;
	}
}