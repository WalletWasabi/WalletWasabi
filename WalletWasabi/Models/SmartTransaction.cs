using NBitcoin;
using Newtonsoft.Json;
using System;
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

		#endregion Members

		#region Constructors

		public SmartTransaction()
		{
		}

		[JsonConstructor]
		public SmartTransaction(Transaction transaction, Height height, string label = "", DateTimeOffset? firstSeenIfMemPoolTime = null)
		{
			Transaction = transaction;
			Label = Guard.Correct(label);

			SetHeight(height);
			if (firstSeenIfMemPoolTime != null)
			{
				FirstSeenIfMemPoolTime = firstSeenIfMemPoolTime;
			}
		}

		public void SetHeight(Height height)
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
		}

		public bool HasLabel() => !string.IsNullOrWhiteSpace(Label);

		#endregion Constructors

		#region Equality

		public bool Equals(SmartTransaction other) => GetHash().Equals(other?.GetHash());

		public bool Equals(Transaction other) => GetHash().Equals(other?.GetHash());

		public override bool Equals(object obj) =>
			(obj is SmartTransaction && this == (SmartTransaction)obj);

		public override int GetHashCode()
		{
			return GetHash().GetHashCode();
		}

		public static bool operator !=(SmartTransaction tx1, SmartTransaction tx2)
		{
			return !(tx1 == tx2);
		}

		public static bool operator ==(SmartTransaction tx1, SmartTransaction tx2)
		{
			bool rc;

			if (ReferenceEquals(tx1, tx2)) rc = true;
			else if ((object)tx1 is null || (object)tx2 is null)
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

			if ((object)tx1 is null || (object)tx2 is null)
			{
				rc = false;
			}
			else
			{
				rc = tx1.GetHash().Equals(tx2.GetHash());
			}

			return rc;
		}

		public static bool operator !=(Transaction tx1, SmartTransaction tx2)
		{
			return !(tx1 == tx2);
		}

		public static bool operator ==(SmartTransaction tx1, Transaction tx2)
		{
			bool rc;

			if ((object)tx1 is null || (object)tx2 is null)
			{
				rc = false;
			}
			else
			{
				rc = tx1.GetHash().Equals(tx2.GetHash());
			}

			return rc;
		}

		public static bool operator !=(SmartTransaction tx1, Transaction tx2)
		{
			return !(tx1 == tx2);
		}

		#endregion Equality
	}
}
