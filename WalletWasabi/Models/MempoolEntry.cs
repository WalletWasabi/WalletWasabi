using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Models
{
	public class MempoolEntry
	{
		public uint256 TransactionId { get; }
		public int VirtualSizeBytes { get; }
		public DateTimeOffset Time { get; }
		public int Height { get; }
		public int DescendantCount { get; }
		public int DescendantVirtualSizeBytes { get; }
		public int AncestorCount { get; }
		public int AncestorVirtualSizeBytes { get; }
		public uint256 TransactionIdWithWitness { get; }
		public Money BaseFee { get; }
		public Money ModifiedFee { get; }
		public Money AncestorFees { get; }
		public Money DescendantFees { get; }
		public IEnumerable<uint256> Dependents { get; }
		public IEnumerable<uint256> SpentBy { get; }

		/// <param name="transactionId">The transaction id (must be in mempool.)</param>
		/// <param name="virtualSizeBytes">Virtual transaction size as defined in BIP 141. This is different from actual serialized size for witness transactions as witness data is discounted.</param>
		/// <param name="time">Local time transaction entered pool in seconds since 1 Jan 1970 GMT.</param>
		/// <param name="height">Block height when transaction entered pool.</param>
		/// <param name="descendantCount">Number of in-mempool descendant transactions (including this one.)</param>
		/// <param name="descendantVirtualSizeBytes">Virtual transaction size of in-mempool descendants (including this one.)</param>
		/// <param name="ancestorCount">Number of in-mempool ancestor transactions (including this one.)</param>
		/// <param name="ancestorVirtualSizeBytes">Virtual transaction size of in-mempool ancestors (including this one.)</param>
		/// <param name="transactionIdWithWitness">Hash of serialized transaction, including witness data.</param>
		/// <param name="baseFee">Transaction fee.</param>
		/// <param name="modifiedFee">Transaction fee with fee deltas used for mining priority.</param>
		/// <param name="ancestorFees">Modified fees (see above) of in-mempool ancestors (including this one.)</param>
		/// <param name="descendantFees">Modified fees (see above) of in-mempool descendants (including this one.)</param>
		/// <param name="dependents">Unconfirmed transactions used as inputs for this transaction;</param>
		/// <param name="spentBy">Unconfirmed transactions spending outputs from this transaction.</param>
		public MempoolEntry(uint256 transactionId, int virtualSizeBytes, DateTimeOffset time, int height, int descendantCount, int descendantVirtualSizeBytes, int ancestorCount, int ancestorVirtualSizeBytes, uint256 transactionIdWithWitness, Money baseFee, Money modifiedFee, Money ancestorFees, Money descendantFees, IEnumerable<uint256> dependents, IEnumerable<uint256> spentBy)
		{
			TransactionId = transactionId ?? throw new ArgumentNullException(nameof(transactionId));
			VirtualSizeBytes = virtualSizeBytes;
			Time = time;
			Height = height;
			DescendantCount = descendantCount;
			DescendantVirtualSizeBytes = descendantVirtualSizeBytes;
			AncestorCount = ancestorCount;
			AncestorVirtualSizeBytes = ancestorVirtualSizeBytes;
			TransactionIdWithWitness = transactionIdWithWitness ?? throw new ArgumentNullException(nameof(transactionIdWithWitness));
			BaseFee = baseFee ?? throw new ArgumentNullException(nameof(baseFee));
			ModifiedFee = modifiedFee ?? throw new ArgumentNullException(nameof(modifiedFee));
			AncestorFees = ancestorFees ?? throw new ArgumentNullException(nameof(ancestorFees));
			DescendantFees = descendantFees ?? throw new ArgumentNullException(nameof(descendantFees));
			Dependents = dependents ?? throw new ArgumentNullException(nameof(dependents));
			SpentBy = spentBy ?? throw new ArgumentNullException(nameof(spentBy));
		}
	}
}
