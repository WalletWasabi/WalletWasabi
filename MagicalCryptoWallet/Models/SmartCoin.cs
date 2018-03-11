using MagicalCryptoWallet.Converters;
using MagicalCryptoWallet.Helpers;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MagicalCryptoWallet.Models
{
	/// <summary>
	/// An UTXO that knows more.
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public class SmartCoin : IEquatable<SmartCoin>
	{
		[JsonProperty(Order = 1)]
		[JsonConverter(typeof(Uint256Converter))]
		public uint256 TransactionId { get; }

		[JsonProperty(Order = 2)]
		public int Index { get; }

		[JsonProperty(Order = 3)]
		[JsonConverter(typeof(ScriptConverter))]
		public Script ScriptPubKey { get; }

		[JsonProperty(Order = 4)]
		[JsonConverter(typeof(MoneyConverter))]
		public Money Amount { get; }

		[JsonProperty(Order = 5)]
		[JsonConverter(typeof(HeightConverter))]
		public Height Height { get; set; }

		[JsonProperty(Order = 6)]
		public string Label { get; set; }

		[JsonProperty(Order = 7)]
		[JsonConverter(typeof(Uint256Converter))]
		public uint256 SpenderTransactionId { get; set; }

		[JsonProperty(Order = 8)]
		public TxoRef[] SpentOutputs { get; }

		public bool IsSpent => SpenderTransactionId == null;

		[JsonConstructor]
		public SmartCoin(uint256 transactionId, int index, Script scriptPubKey, Money amount, TxoRef[] spentOutputs, Height height, string label = "", uint256 spenderTransactionId = null)
		{
			TransactionId = Guard.NotNull(nameof(transactionId), transactionId);
			Index = Guard.NotNull(nameof(index), index);
			ScriptPubKey = Guard.NotNull(nameof(scriptPubKey), scriptPubKey);
			Amount = Guard.NotNull(nameof(amount), amount);
			SpentOutputs = Guard.NotNull(nameof(spentOutputs), spentOutputs);
			Height = height;
			Label = string.IsNullOrWhiteSpace(label) ? "" : label.Trim();
			SpenderTransactionId = spenderTransactionId;
		}

		#region EqualityAndComparison

		public override bool Equals(object obj) => obj is SmartCoin && this == (SmartCoin)obj;
		public bool Equals(SmartCoin other) => this == other;
		public override int GetHashCode() => TransactionId.GetHashCode() ^ Index;
		public static bool operator ==(SmartCoin x, SmartCoin y) => y.TransactionId == x.TransactionId && y.Index == x.Index;
		public static bool operator !=(SmartCoin x, SmartCoin y) => !(x == y);

		#endregion
	}
}
