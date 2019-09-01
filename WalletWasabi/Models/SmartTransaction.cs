using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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

		public int GetConfirmationCount(Height bestHeight) => Height == Height.Mempool ? 0 : bestHeight.Value - Height.Value + 1;

		[JsonProperty]
		[JsonConverter(typeof(DateTimeOffsetUnixSecondsConverter))]
		public DateTimeOffset FirstSeen { get; private set; }

		[JsonProperty(PropertyName = "FirstSeenIfMempoolTime")]
		[JsonConverter(typeof(BlockCypherDateTimeOffsetJsonConverter))]
		[Obsolete("This property exists only for json backwards compatibility. If someone tries to set it, it'll set the FirstSeen. https://stackoverflow.com/a/43715009/2061103", error: true)]
#pragma warning disable IDE0051 // Remove unused private members
		private DateTimeOffset? FirstSeenCompatibility
#pragma warning restore IDE0051 // Remove unused private members
		{
			set
			{
				// If it's null, let FirstSeen's default to be set.
				// If it's not null, then check if FirsSeen has just been recently set to utcnow which is its default.
				if (value.HasValue && (DateTimeOffset.UtcNow - FirstSeen) < TimeSpan.FromSeconds(1))
				{
					FirstSeen = value.Value;
				}
			}
		}

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

		public SmartTransaction(Transaction transaction, Height height, uint256 blockHash = null, int blockIndex = 0, string label = "", bool isReplacement = false, DateTimeOffset firstSeen = default)
		{
			Transaction = transaction;
			Label = Guard.Correct(label);

			SetHeight(height, blockHash, blockIndex);

			FirstSeen = firstSeen == default ? DateTimeOffset.UtcNow : firstSeen;

			IsReplacement = isReplacement;
		}

		#endregion Constructors

		public void SetHeight(Height height, uint256 blockHash = null, int blockIndex = 0)
		{
			Height = height;
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

				var firstSeenCompareResult = a.FirstSeen.CompareTo(b.FirstSeen);
				return firstSeenCompareResult;
			});
		}

		#region Equality

		public bool Equals(SmartTransaction other) => GetHash().Equals(other?.GetHash());

		public bool Equals(Transaction other) => GetHash().Equals(other?.GetHash());

		public override bool Equals(object obj) =>
			obj is SmartTransaction transaction && this == transaction;

		public override int GetHashCode() => GetHash().GetHashCode();

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
			bool rc = tx1 is null || tx2 is null ? false : tx1.GetHash().Equals(tx2.GetHash());
			return rc;
		}

		public static bool operator !=(Transaction tx1, SmartTransaction tx2) => !(tx1 == tx2);

		public static bool operator ==(SmartTransaction tx1, Transaction tx2)
		{
			bool rc = tx1 is null || tx2 is null ? false : tx1.GetHash().Equals(tx2.GetHash());
			return rc;
		}

		public static bool operator !=(SmartTransaction tx1, Transaction tx2) => !(tx1 == tx2);

		#endregion Equality
	}
}
