using NBitcoin;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Crypto;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

// OwnershipProofValidator validates the ownership proofs provided by the coordinator.
public class OwnershipProofValidator : IOwnershipProofValidator 
{
	public OwnershipProofValidator(TransactionStore transactionStore, IBlockProvider blockProvider)
	{
		TransactionStore = transactionStore;
		BlockProvider = blockProvider;
	}

	private TransactionStore TransactionStore { get; }
	private IBlockProvider BlockProvider { get; }

	// Verifies the coins and ownership proofs. These proofs are provided by the coordinator who should
	// have validated them before which means that when dealing with a honest coordinator all these proofs
	// are valid.
	//
	// In case one proof is invalid we have evidence enough to know the coordinator is malicious and we
	// have to abort the process, notify the user and ban the coordinator.
	public async ValueTask<int> VerifyOtherAlicesOwnershipProofsAsync(
		CoinJoinInputCommitmentData coinJoinInputCommitmentData,
		ImmutableList<(uint256 BlockId, OutPoint OutPoint, Money Amount, OwnershipProof OwnershipProof)> othersCoins,
		int minimumNumberOfValidProofs,
		CancellationToken cancellationToken)
	{
		var validProofs = 0;
		
		// What follows is an optimization where we try to verify coins that are already known by us.
		var mineTxIds = TransactionStore.GetTransactionHashes();
		var alreadySeenCoins = othersCoins.Where(x => mineTxIds.Contains(x.OutPoint.Hash));

		// In case one Alice is trying to spend an output from a transaction that we already have
		// seen before then we can verify its script without downloading anything from the network.
		// However it is impossible for us to know it the coins is unspent!.
		using var alreadySeenCoinsEnumerator = alreadySeenCoins.GetEnumerator();

		while (alreadySeenCoinsEnumerator.MoveNext() && validProofs < minimumNumberOfValidProofs)
		{
			var (_, outpoint, amount, ownershipProof) = alreadySeenCoinsEnumerator.Current;
			if (TransactionStore.TryGetTransaction(outpoint.Hash, out var stx))
			{
				var coinIndex = (int) outpoint.N;
				var outputs = stx.Transaction.Outputs;
				if (coinIndex >= outputs.Count || outputs[coinIndex].Value != amount)
				{
					throw new MaliciousCoordinatorException("Fake coin with impossible index or different amount.");
				}

				var foundCoin = new Coin(stx.Transaction, stx.Transaction.Outputs[coinIndex]);
				VerifyCoin(foundCoin.ScriptPubKey, coinJoinInputCommitmentData, ownershipProof);
				validProofs++;
			}
		}

		// In case we cannot validate enough proofs using only our already seen transactions, we
		// would need to start downloading the blocks containing the coins that need to be
		// validated.
		var coinsGroupedByBlocks = othersCoins
			.GroupBy(x => x.BlockId)
			.Select(x => (BlockId: x.Key, OutPoints: x))
			.OrderBy(x => Random.Shared.Next())
			.ToList();

		using var coinsGroupedByBlocksEnumerator = coinsGroupedByBlocks.GetEnumerator();
		while (coinsGroupedByBlocksEnumerator.MoveNext() && validProofs < minimumNumberOfValidProofs)
		{
			var (blockId, coinsInBlock) = coinsGroupedByBlocksEnumerator.Current; 
			var block = await BlockProvider.GetBlockAsync(blockId, cancellationToken).ConfigureAwait(false);

			if (block is { })
			{
				foreach (var (_, outpoint, amount, ownershipProof) in coinsInBlock)
				{
					var tx = block.Transactions.FirstOrDefault(x => x.GetHash() == outpoint.Hash);
					if (tx is null)
					{
						throw new MaliciousCoordinatorException(
							$"There is not transaction with id '{outpoint.Hash}' in block '{blockId}').");
					}

					var coinIndex = (int) outpoint.N;
					if (coinIndex >= tx.Outputs.Count || tx.Outputs[coinIndex].Value != amount)
					{
						throw new MaliciousCoordinatorException("Fake coin with impossible index or different amount.");
					}

					var foundCoin = new Coin(tx, tx.Outputs[coinIndex]);
					VerifyCoin(foundCoin.ScriptPubKey, coinJoinInputCommitmentData, ownershipProof);
					validProofs++;
				}
			}
			else
			{
				// FIXME: what if no peer has the block?
			}
		}

		return validProofs;
	}

	private static void VerifyCoin(Script scriptPubKey, CoinJoinInputCommitmentData coinJoinInputCommitmentData, OwnershipProof ownershipProof)
	{
		if (!OwnershipProof.VerifyCoinJoinInputProof(ownershipProof, scriptPubKey, coinJoinInputCommitmentData))
		{
			throw new MaliciousCoordinatorException("The ownership proof is not valid which means Alice cannot really spend it).");
		}
	}
}

public interface IOwnershipProofValidator
{
	ValueTask<int> VerifyOtherAlicesOwnershipProofsAsync(
		CoinJoinInputCommitmentData coinJoinInputCommitmentData,
		ImmutableList<(uint256 BlockId, OutPoint OutPoint, Money Amount, OwnershipProof OwnershipProof)> othersCoins,
		int minimumNumberOfValidProofs,
		CancellationToken cancellationToken);
}
