using WalletWasabi.JsonConverters;
using WalletWasabi.Helpers;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WalletWasabi.Models
{
	/// <summary>
	/// An UTXO that knows more.
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public class SmartCoin : IEquatable<SmartCoin>
	{
		[JsonProperty(Order = 1)]
		[JsonConverter(typeof(Uint256JsonConverter))]
		public uint256 TransactionId { get; }

		[JsonProperty(Order = 2)]
		public int Index { get; }

		[JsonProperty(Order = 3)]
		[JsonConverter(typeof(JsonConverters.ScriptJsonConverter))]
		public Script ScriptPubKey { get; }

		[JsonProperty(Order = 4)]
		[JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money Amount { get; }

		[JsonProperty(Order = 5)]
		[JsonConverter(typeof(HeightJsonConverter))]
		public Height Height { get; set; }

		[JsonProperty(Order = 6)]
		public string Label { get; set; }

		[JsonProperty(Order = 7)]
		[JsonConverter(typeof(Uint256JsonConverter))]
		public uint256 SpenderTransactionId { get; set; }

		[JsonProperty(Order = 8)]
		public TxoRef[] SpentOutputs { get; }

		[JsonProperty(Order = 9)]
		[JsonConverter(typeof(FunnyBoolJsonConverter))]
		public bool RBF { get; }

		[JsonProperty(Order = 10)]
		[JsonConverter(typeof(FunnyBoolJsonConverter))]
		public bool Locked { get; set; }

		[JsonProperty(Order = 11)]
		public int Mixin { get; }

		public bool SpentOrLocked => SpenderTransactionId != null || Locked;
		public bool Unspent => SpenderTransactionId == null;
		public bool Confirmed => Height != Height.MemPool && Height != Height.Unknown;

		/// <summary>
		/// It's a secret, so it's usually going to be null. Don't use it.
		/// </summary>
		public ISecret Secret { get; set; }

		[JsonConstructor]
		public SmartCoin(uint256 transactionId, int index, Script scriptPubKey, Money amount, TxoRef[] spentOutputs, Height height, bool rbf, int mixin, string label = "", uint256 spenderTransactionId = null, bool locked = false)
		{
			TransactionId = Guard.NotNull(nameof(transactionId), transactionId);
			Index = Guard.NotNull(nameof(index), index);
			ScriptPubKey = Guard.NotNull(nameof(scriptPubKey), scriptPubKey);
			Amount = Guard.NotNull(nameof(amount), amount);
			SpentOutputs = Guard.NotNull(nameof(spentOutputs), spentOutputs);
			Mixin = Guard.InRangeAndNotNull(nameof(mixin), mixin, 0, int.MaxValue);
			Height = height;
			Label = Guard.Correct(label);
			SpenderTransactionId = spenderTransactionId;
			RBF = rbf;
			Locked = locked;
			Secret = null;
		}

		public Coin GetCoin()
		{
			return new Coin(TransactionId, (uint)Index, Amount, ScriptPubKey);
		}

		public OutPoint GetOutPoint()
		{
			return new OutPoint(TransactionId, (uint)Index);
		}

		#region EqualityAndComparison

		public override bool Equals(object obj) => obj is SmartCoin && this == (SmartCoin)obj;

		public bool Equals(SmartCoin other) => this == other;

		public override int GetHashCode() => TransactionId.GetHashCode() ^ Index;

		public static bool operator ==(SmartCoin x, SmartCoin y) => y?.TransactionId == x?.TransactionId && y?.Index == x?.Index;

		public static bool operator !=(SmartCoin x, SmartCoin y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
