using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Crypto;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client
{
	// OwnershipProofValidator validates the ownership proofs provided by the coordinator.
	public class OwnershipProofValidator
	{
		public OwnershipProofValidator(IndexStore indexStore, TransactionStore transactionStore, IBlockProvider blockProvider)
		{
			IndexStore = indexStore;
			TransactionStore = transactionStore;
			BlockProvider = blockProvider;
		}

		public IndexStore IndexStore { get; }
		public TransactionStore TransactionStore { get; }
		public IBlockProvider BlockProvider { get; }

		// Verifies the coins and ownership proofs. These proofs are provided by the coordinator who should
		// have validated them before which means that when dealing with an honest coordinator all these proofs
		// are valid.
		//
		// In case one proof is invalid we have evidence enough to know the coordinator is malicious and we
		// have to abort the process, notify the user and ban the coordinator.
		public async ValueTask<int> VerifyOtherAlicesOwnershipProofsAsync(
			uint256 roundId,
			IEnumerable<(Coin Coin, OwnershipProof OwnershipProof)> othersCoins,
			int minimumNumberOfCoinsToValidate,
			CancellationToken cancellationToken)
		{
			var proofChannel = Channel.CreateBounded<(Coin, Coin?, OwnershipProof)>(10);
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

			var mineTxIds = TransactionStore.GetTransactionHashes();
			var alreadySeenCoins = othersCoins.Where(x => mineTxIds.Contains(x.Coin.Outpoint.Hash));

			// In case one Alice is trying to spend an output from a transaction that we already have
			// seen before then we can verify its script without downloading anything from the network.
			foreach (var (coin, ownershipProof) in alreadySeenCoins)
			{
				if (TransactionStore.TryGetTransaction(coin.Outpoint.Hash, out var stx))
				{
					var transactionCoins = stx.Transaction.Outputs.AsCoins();
					var foundCoin = transactionCoins.ElementAtOrDefault((int)coin.Outpoint.N);
					await proofChannel.Writer.WriteAsync((coin, foundCoin, ownershipProof), cts.Token).ConfigureAwait(false);
				}
			}

			// In case there are no filters.
			if (IndexStore.SmartHeaderChain.HashCount == 0)
			{
				proofChannel.Writer.Complete();
			}

			// In case we cannot validate enough proofs using only our already seen transactions, we
			// would need to start downloading the blocks containing the coins that need to be
			// validated. We use our block filters for that.
			var scripts = othersCoins.Select(x => x.Coin.ScriptPubKey.ToCompressedBytes()).ToArray();
			_ = Task.Run(async () =>
			{
				await IndexStore.ForeachFiltersAsync(async (filterModel) =>
				{
					var matchFound = filterModel.Filter.MatchAny(scripts, filterModel.FilterKey);
					if (matchFound)
					{
						var blockId = filterModel.Header.BlockHash;
						var block = await BlockProvider.GetBlockAsync(blockId, cts.Token).ConfigureAwait(false);

						foreach (var (coin, ownershipProof) in othersCoins)
						{
							if (block?.Transactions.FirstOrDefault(x => x.GetHash() == coin.Outpoint.Hash) is { } tx)
							{
								var transactionCoins = tx.Outputs.AsCoins();
								var foundCoin = transactionCoins.ElementAtOrDefault((int)coin.Outpoint.N);
								await proofChannel.Writer.WriteAsync((coin, foundCoin, ownershipProof), cts.Token).ConfigureAwait(false);
							}
						}
					}
					// there are no more filters
					if (filterModel.Header.BlockHash == IndexStore.SmartHeaderChain.TipHash)
					{
						proofChannel.Writer.Complete();
					}
				}, 0, cts.Token).ConfigureAwait(false);
			});

			// Consumes and validates the coins and their proofs.
			var validProofs = 0;
			var coinJoinInputCommitmentData = new CoinJoinInputCommitmentData("CoinJoinCoordinatorIdentifier", roundId);
			while (validProofs < minimumNumberOfCoinsToValidate && await proofChannel.Reader.WaitToReadAsync(cts.Token).ConfigureAwait(false))
			{
				var (aliceCoin, realCoin, ownershipProof) = await proofChannel.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
				if (realCoin is not { } coin || (aliceCoin.TxOut, aliceCoin.Outpoint) != (coin.TxOut, coin.Outpoint))
				{
					cts.Cancel();
					throw new InvalidOperationException("The coordinator lies (coin doesn't exists or is different than the provided by the coordinator).");
				}
				if (!OwnershipProof.VerifyCoinJoinInputProof(ownershipProof, aliceCoin.ScriptPubKey, coinJoinInputCommitmentData))
				{
					cts.Cancel();
					throw new InvalidOperationException("The coordinator lies (the ownership proof is not valid what means Alice cannot really spend it).");
				}
				validProofs++;
			}

			cts.Cancel();
			return validProofs;
		}
	}
}
