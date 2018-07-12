using NBitcoin;
using Newtonsoft.Json;
using System;
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

		public bool Confirmed => Height.Type == HeightType.Chain;

		public uint256 GetHash() => Transaction.GetHash();

		public int GetConfirmationCount(Height bestHeight) => Height == Height.MemPool ? 0 : bestHeight.Value - Height.Value + 1;

		/// <summary>
		/// if Height is MemPool it's first seen, else null,
		/// only exists in memory,
		/// doesn't affect equality
		/// </summary>
		public DateTimeOffset? FirstSeenIfMemPoolTime { get; private set; }

		#endregion Members

		#region Constructors

		public SmartTransaction()
		{
		}

		[JsonConstructor]
		public SmartTransaction(Transaction transaction, Height height)
		{
			Transaction = transaction;

			SetHeight(height);
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

		#endregion Constructors

		#region Equality

		public bool Equals(SmartTransaction other) => Equals((object)other);

		public bool Equals(Transaction other) => Equals((object)other);

		public override bool Equals(object obj)
		{
			if(object.ReferenceEquals(this, obj))
				return true;
			
			if(obj is SmartTransaction st)
			{
				return st.Transaction.GetHash() == this.GetHash();
			}
			else if(obj is Transaction t)
			{
				return t.GetHash() == this.GetHash();
			}
			return false;
		}

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
			return tx1.Equals(tx2);
		}

		public static bool operator ==(Transaction tx1, SmartTransaction tx2)
		{
			return tx2.Equals(tx1);
		}

		public static bool operator !=(Transaction tx1, SmartTransaction tx2)
		{
			return !(tx1 == tx2);
		}

		public static bool operator ==(SmartTransaction tx1, Transaction tx2)
		{
			return tx1.Equals(tx2);
		}

		public static bool operator !=(SmartTransaction tx1, Transaction tx2)
		{
			return !(tx1 == tx2);
		}

		#endregion Equality
	}
}
