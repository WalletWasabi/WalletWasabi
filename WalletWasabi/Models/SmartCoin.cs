using WalletWasabi.JsonConverters;
using WalletWasabi.Helpers;
using NBitcoin;
using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace WalletWasabi.Models
{
	/// <summary>
	/// An UTXO that knows more.
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public class SmartCoin : IEquatable<SmartCoin>, INotifyPropertyChanged
	{
		private Height _height;

		[JsonProperty(Order = 1)]
		[JsonConverter(typeof(Uint256JsonConverter))]
		public uint256 TransactionId { get; }

		[JsonProperty(Order = 2)]
		public uint Index { get; }

		[JsonProperty(Order = 3)]
		[JsonConverter(typeof(ScriptJsonConverter))]
		public Script ScriptPubKey { get; }

		[JsonProperty(Order = 4)]
		[JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money Amount { get; }

		[JsonProperty(Order = 5)]
		[JsonConverter(typeof(HeightJsonConverter))]
		public Height Height
		{
			get { return _height; }
			set
			{
				if (value != _height)
				{
					_height = value;

					PropertyChanged(this, new PropertyChangedEventArgs(nameof(Confirmed)));
				}
			}
		}

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
		public bool CoinJoinInProcess { get; set; }

		/// <summary>
		/// AnonymitySet - 1
		/// </summary>
		[JsonProperty(Order = 11)]
		public int Mixin { get; }

		public bool SpentOrCoinJoinInProcess => SpenderTransactionId != null || CoinJoinInProcess;
		public bool Unspent => SpenderTransactionId == null;
		public bool Confirmed => Height != Height.MemPool && Height != Height.Unknown;

		/// <summary>
		/// Mixin + 1
		/// </summary>
		public int AnonymitySet => Mixin + 1;

		/// <summary>
		/// It's a secret, so it's usually going to be null. Don't use it.
		/// </summary>
		public ISecret Secret { get; set; }

		[JsonConstructor]
		public SmartCoin(uint256 transactionId, uint index, Script scriptPubKey, Money amount, TxoRef[] spentOutputs, Height height, bool rbf, int mixin, string label = "", uint256 spenderTransactionId = null, bool locked = false)
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
			CoinJoinInProcess = locked;
			Secret = null;
		}

		public SmartCoin(Coin coin, TxoRef[] spentOutputs, Height height, bool rbf, int mixin, string label = "", uint256 spenderTransactionId = null, bool locked = false)
		{
			Guard.NotNull(nameof(coin), coin);
			TransactionId = coin.Outpoint.Hash;
			Index = coin.Outpoint.N;
			ScriptPubKey = coin.ScriptPubKey;
			Amount = coin.Amount;
			SpentOutputs = Guard.NotNull(nameof(spentOutputs), spentOutputs);
			Mixin = Guard.InRangeAndNotNull(nameof(mixin), mixin, 0, int.MaxValue);
			Height = height;
			Label = Guard.Correct(label);
			SpenderTransactionId = spenderTransactionId;
			RBF = rbf;
			CoinJoinInProcess = locked;
			Secret = null;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public Coin GetCoin()
		{
			return new Coin(TransactionId, Index, Amount, ScriptPubKey);
		}

		public OutPoint GetOutPoint()
		{
			return new OutPoint(TransactionId, Index);
		}

		public TxoRef GetTxoRef()
		{
			return new TxoRef(TransactionId, Index);
		}

		#region EqualityAndComparison

		public override bool Equals(object obj) => obj is SmartCoin && this == (SmartCoin)obj;

		public bool Equals(SmartCoin other) => this == other;

		public override int GetHashCode() => TransactionId.GetHashCode() ^ (int)Index;

		public static bool operator ==(SmartCoin x, SmartCoin y) => y?.TransactionId == x?.TransactionId && y?.Index == x?.Index;

		public static bool operator !=(SmartCoin x, SmartCoin y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
