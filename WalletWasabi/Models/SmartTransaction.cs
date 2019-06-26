using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Models
{
	[JsonObject(MemberSerialization.OptIn)]
	public class SmartTransaction : IEquatable<SmartTransaction>, IEquatable<Transaction>
	{
		#region Members

		[JsonProperty]
		[JsonConverter(typeof(TransactionJsonConverter))]
		public Transaction Transaction { get; }

		[JsonProperty]
		[JsonConverter(typeof(HeightJsonConverter))]
		public Height Height { get; private set; }

		[JsonProperty]
		[JsonConverter(typeof(Uint256JsonConverter))]
		public uint256 BlockHash { get; private set; }

		[JsonProperty]
		public int BlockIndex { get; private set; }

		[JsonProperty]
		public string Label { get; set; }

		public bool Confirmed => Height.Type == HeightType.Chain;

		public uint256 GetHash() => Transaction.GetHash();

		public int GetConfirmationCount(Height bestHeight) => Height == Height.MemPool ? 0 : bestHeight.Value - Height.Value + 1;

		/// <summary>
		/// if Height is MemPool it's first seen, else null,
		/// only exists in memory,
		/// doesn't affect equality
		/// </summary>
		[JsonProperty]
		[JsonConverter(typeof(BlockCypherDateTimeOffsetJsonConverter))]
		public DateTimeOffset? FirstSeenIfMemPoolTime { get; private set; }

		[JsonProperty]
		public bool IsReplacement { get; private set; }

		/// <summary>
		/// A transaction can signal that is replaceable by fee in two ways:
		/// * Explicitly by using a nSequence &lt; (0xffffffff - 1) or,
		/// * Implicitly in case one of its unconfirmed ancestors are replaceable
		/// </summary>
		public bool IsRBF => !Confirmed && (Transaction.RBF || IsReplacement);

		#endregion Members

		#region Constructors

		public SmartTransaction()
		{
		}

		[JsonConstructor]
		public SmartTransaction(Transaction transaction, Height height, uint256 blockHash = null, int blockIndex = 0, string label = "", DateTimeOffset? firstSeenIfMemPoolTime = null, bool isReplacement = false)
		{
			Transaction = transaction;
			Label = Guard.Correct(label);

			SetHeight(height, blockHash, blockIndex);
			if (firstSeenIfMemPoolTime != null)
			{
				FirstSeenIfMemPoolTime = firstSeenIfMemPoolTime;
			}
			IsReplacement = isReplacement;
		}

		#endregion Constructors

		public void SetHeight(Height height, uint256 blockHash = null, int blockIndex = 0)
		{
			Height = height;
			if (height == Height.MemPool)
			{
				FirstSeenIfMemPoolTime = DateTimeOffset.UtcNow;
			}
			else
			{
				FirstSeenIfMemPoolTime = null;
			}

			BlockHash = blockHash;
			BlockIndex = blockIndex;
		}

		public void SetReplacement()
		{
			IsReplacement = true;
		}

		public bool HasLabel() => !string.IsNullOrWhiteSpace(Label);

		/// <summary>
		/// First looks at height, then block index, then mempool firstseen.
		/// </summary>
		public static IComparer<SmartTransaction> GetBlockchainComparer()
		{
			return Comparer<SmartTransaction>.Create((a, b) =>
			{
				var heightCompareResult = a.Height.CompareTo(b.Height);
				if (heightCompareResult != 0)
				{
					return heightCompareResult;
				}

				// If mempool this should be 0, so they should be equal so no worry about it.
				var blockIndexCompareResult = a.BlockIndex.CompareTo(b.BlockIndex);
				if (blockIndexCompareResult != 0)
				{
					return blockIndexCompareResult;
				}

				var firstSeenCompareResult = (a.FirstSeenIfMemPoolTime ?? DateTime.UtcNow).CompareTo(b.FirstSeenIfMemPoolTime ?? DateTime.UtcNow);
				return firstSeenCompareResult;
			});
		}

		#region Equality

		public bool Equals(SmartTransaction other) => GetHash().Equals(other?.GetHash());

		public bool Equals(Transaction other) => GetHash().Equals(other?.GetHash());

		public override bool Equals(object obj) =>
			(obj is SmartTransaction && this == (SmartTransaction)obj);

		public override int GetHashCode()
		{
			return GetHash().GetHashCode();
		}

		public static bool operator !=(SmartTransaction tx1, SmartTransaction tx2) => !(tx1 == tx2);

		public static bool operator ==(SmartTransaction tx1, SmartTransaction tx2)
		{
			bool rc;

			if (ReferenceEquals(tx1, tx2))
			{
				rc = true;
			}
			else if (tx1 is null || tx2 is null)
			{
				rc = false;
			}
			else
			{
				rc = tx1.GetHash().Equals(tx2.GetHash());
			}

			return rc;
		}

		public static bool operator ==(Transaction tx1, SmartTransaction tx2)
		{
			bool rc;

			if (tx1 is null || tx2 is null)
			{
				rc = false;
			}
			else
			{
				rc = tx1.GetHash().Equals(tx2.GetHash());
			}

			return rc;
		}

		public static bool operator !=(Transaction tx1, SmartTransaction tx2) => !(tx1 == tx2);

		public static bool operator ==(SmartTransaction tx1, Transaction tx2)
		{
			bool rc;

			if (tx1 is null || tx2 is null)
			{
				rc = false;
			}
			else
			{
				rc = tx1.GetHash().Equals(tx2.GetHash());
			}

			return rc;
		}

		public static bool operator !=(SmartTransaction tx1, Transaction tx2) => !(tx1 == tx2);

		#endregion Equality
	}
}
