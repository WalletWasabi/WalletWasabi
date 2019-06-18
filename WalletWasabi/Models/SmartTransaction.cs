using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Text;
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
		public SmartTransaction(Transaction transaction, Height height, string label = "", DateTimeOffset? firstSeenIfMemPoolTime = null, bool isReplacement = false)
		{
			Transaction = transaction;
			Label = Guard.Correct(label);

			SetHeight(height);
			if (firstSeenIfMemPoolTime != null)
			{
				FirstSeenIfMemPoolTime = firstSeenIfMemPoolTime;
			}
			IsReplacement = isReplacement;
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

		public void SetReplacement()
		{
			IsReplacement = true;
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

		public string ToLine()
		{
			var builder = new StringBuilder();
			builder.Append(Transaction.ToHex());
			builder.Append(":");
			builder.Append(Height.ToString());
			builder.Append(":");
			builder.Append(Label.Replace(':', ';')); // Just in case;
			builder.Append(":");
			DateTimeOffset fistSeen = FirstSeenIfMemPoolTime ?? DateTimeOffset.UtcNow;
			builder.Append(fistSeen.ToString(CultureInfo.InvariantCulture));
			builder.Append(":");
			builder.Append(IsReplacement);

			return builder.ToString();
		}

		public static SmartTransaction FromLine(string line)
		{
			Guard.NotNullOrEmptyOrWhitespace(nameof(line), line);
			string[] parts = line.Split(':');

			var tx = Transaction.Parse(parts[0], Network.Main);

			Height height;
			if (Height.TryParse(parts[1], out Height h))
			{
				height = h;
			}
			else
			{
				throw new FormatException("Couldn't parse Height.");
			}

			string label = parts[2];

			DateTimeOffset firstSeen = DateTimeOffset.Parse(parts[3], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

			bool isReplacement = bool.Parse(parts[4]);

			return new SmartTransaction(tx, height, label, firstSeen, isReplacement);
		}

		public static bool operator !=(Transaction tx1, SmartTransaction tx2)
		{
			return !(tx1 == tx2);
		}

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

		public static bool operator !=(SmartTransaction tx1, Transaction tx2)
		{
			return !(tx1 == tx2);
		}

		#endregion Equality
	}
}
