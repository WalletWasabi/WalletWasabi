using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.BlockchainAnalysis;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Transactions
{
	[JsonObject(MemberSerialization.OptIn)]
	public class SmartTransaction : IEquatable<SmartTransaction>
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
		[JsonConverter(typeof(SmartLabelJsonConverter))]
		public SmartLabel Label { get; set; }

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
				// If it's not null, then check if FirstSeen has just been recently set to utcnow which is its default.
				if (value.HasValue && DateTimeOffset.UtcNow - FirstSeen < TimeSpan.FromSeconds(1))
				{
					FirstSeen = value.Value;
				}
			}
		}

		[JsonProperty]
		public bool IsReplacement { get; private set; }

		public bool Confirmed => Height.Type == HeightType.Chain;

		public uint256 GetHash() => Transaction.GetHash();

		public int GetConfirmationCount(Height bestHeight) => Height == Height.Mempool ? 0 : bestHeight.Value - Height.Value + 1;

		/// <summary>
		/// A transaction can signal that is replaceable by fee in two ways:
		/// * Explicitly by using a nSequence &lt; (0xffffffff - 1) or,
		/// * Implicitly in case one of its unconfirmed ancestors are replaceable
		/// </summary>
		public bool IsRBF => !Confirmed && (Transaction.RBF || IsReplacement);

		#endregion Members

		#region Constructors

		public SmartTransaction(Transaction transaction, Height height, uint256 blockHash = null, int blockIndex = 0, SmartLabel label = null, bool isReplacement = false, DateTimeOffset firstSeen = default)
		{
			Transaction = transaction;
			// Because we don't modify those transactions, we can cache the hash
			Transaction.PrecomputeHash(false, true);

			Label = label ?? SmartLabel.Empty;

			Height = height;
			BlockHash = blockHash;
			BlockIndex = blockIndex;

			FirstSeen = firstSeen == default ? DateTimeOffset.UtcNow : firstSeen;

			IsReplacement = isReplacement;
		}

		#endregion Constructors

		/// <summary>
		/// Update the transaction with the data acquired from another transaction. (For example merge their labels.)
		/// </summary>
		public bool TryUpdate(SmartTransaction tx)
		{
			var updated = false;
			// If this is not the same tx, then don't update.
			if (this != tx)
			{
				throw new InvalidOperationException($"{GetHash()} != {tx.GetHash()}");
			}

			// Set the height related properties, only if confirmed.
			if (tx.Confirmed)
			{
				if (Height != tx.Height)
				{
					Height = tx.Height;
					updated = true;
				}

				if (tx.BlockHash != null && BlockHash != tx.BlockHash)
				{
					BlockHash = tx.BlockHash;
					BlockIndex = tx.BlockIndex;
					updated = true;
				}
			}

			// Always the earlier seen is the firstSeen.
			if (tx.FirstSeen < FirstSeen)
			{
				FirstSeen = tx.FirstSeen;
				updated = true;
			}

			// Merge labels.
			if (Label != tx.Label)
			{
				Label = SmartLabel.Merge(Label, tx.Label);
				updated = true;
			}

			return updated;
		}

		public void SetReplacement()
		{
			IsReplacement = true;
		}

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

		#region LineSerialization

		public string ToLine()
		{
			// GetHash is also serialized, so file can be interpreted with our eyes better.

			return string.Join(':',
				GetHash(),
				Transaction.ToHex(),
				Height,
				BlockHash,
				BlockIndex,
				Label,
				FirstSeen.ToUnixTimeSeconds(),
				IsReplacement);
		}

		public static SmartTransaction FromLine(string line, Network expectedNetwork)
		{
			line = Guard.NotNullOrEmptyOrWhitespace(nameof(line), line, trim: true);
			expectedNetwork = Guard.NotNull(nameof(expectedNetwork), expectedNetwork);

			var parts = line.Split(':', StringSplitOptions.None).Select(x => x.Trim()).ToArray();

			var transactionString = parts[1];
			Transaction transaction = Transaction.Parse(transactionString, expectedNetwork);

			try
			{
				// First is redundand txhash serialization.
				var heightString = parts[2];
				var blockHashString = parts[3];
				var blockIndexString = parts[4];
				var labelString = parts[5];
				var firstSeenString = parts[6];
				var isReplacementString = parts[7];

				if (!Height.TryParse(heightString, out Height height))
				{
					height = Height.Unknown;
				}
				if (!uint256.TryParse(blockHashString, out uint256 blockHash))
				{
					blockHash = null;
				}
				if (!int.TryParse(blockIndexString, out int blockIndex))
				{
					blockIndex = 0;
				}
				var label = new SmartLabel(labelString);
				DateTimeOffset firstSeen = default;
				if (long.TryParse(firstSeenString, out long unixSeconds))
				{
					firstSeen = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
				}
				if (!bool.TryParse(isReplacementString, out bool isReplacement))
				{
					isReplacement = false;
				}

				return new SmartTransaction(transaction, height, blockHash, blockIndex, label, isReplacement, firstSeen);
			}
			catch (Exception ex)
			{
				Logger.LogDebug(ex);
				return new SmartTransaction(transaction, Height.Unknown);
			}
		}

		#endregion LineSerialization

		#region EqualityAndComparison

		public override bool Equals(object obj) => obj is SmartTransaction tx && this == tx;

		public bool Equals(SmartTransaction other) => this == other;

		public override int GetHashCode() => GetHash().GetHashCode();

		public static bool operator ==(SmartTransaction x, SmartTransaction y) => y?.GetHash() == x?.GetHash();

		public static bool operator !=(SmartTransaction x, SmartTransaction y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
