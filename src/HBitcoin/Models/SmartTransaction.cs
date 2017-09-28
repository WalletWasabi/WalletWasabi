using System;
using NBitcoin;

namespace HBitcoin.Models
{
	public class SmartTransaction: IEquatable<SmartTransaction>
	{
		#region Members
		
		public Height Height { get; }
		public Transaction Transaction { get; }

		public bool Confirmed => Height.Type == HeightType.Chain;
		public uint256 GetHash() => Transaction.GetHash();

		public int GetConfirmationCount(Height bestHeight) => Height == Height.MemPool ? 0 : bestHeight.Value - Height.Value + 1;


		private readonly DateTimeOffset? _firstSeenIfMemPoolHeight = null;
        /// <summary>
        /// if Height is MemPool it's first seen, else null, 
        /// only exists in memory,
        /// doesn't affect equality
        /// </summary>
        public DateTimeOffset? GetFirstSeenIfMemPoolHeight() => _firstSeenIfMemPoolHeight;

		#endregion

		#region Constructors

		public SmartTransaction()
		{

		}
		
		public SmartTransaction(Transaction transaction, Height height)
		{
			Height = height;
			Transaction = transaction;

            if (height == Height.MemPool)
                _firstSeenIfMemPoolHeight = DateTimeOffset.UtcNow;
		}

		#endregion

		#region Equality

		public bool Equals(SmartTransaction other) => GetHash().Equals(other.GetHash());
		public bool Equals(Transaction other) => GetHash().Equals(other.GetHash());

		public override bool Equals(object obj)
		{
			bool rc = false;


            if (obj is SmartTransaction st)
            {
                rc = GetHash().Equals(st.GetHash());
            }
            else if (obj is Transaction t)
            {
                rc = GetHash().Equals(t.GetHash());
            }
            return rc;
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
			bool rc;

			if(ReferenceEquals(tx1, tx2)) rc = true;

			else if((object) tx1 == null || (object) tx2 == null)
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

			if ((object)tx1 == null || (object)tx2 == null)
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

			if ((object)tx1 == null || (object)tx2 == null)
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
		
		#endregion
	}
}
