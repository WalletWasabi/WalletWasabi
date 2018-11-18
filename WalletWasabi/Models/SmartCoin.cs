using WalletWasabi.JsonConverters;
using WalletWasabi.Helpers;
using NBitcoin;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace WalletWasabi.Models
{
	/// <summary>
	/// An UTXO that knows more.
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public class SmartCoin : IEquatable<SmartCoin>, INotifyPropertyChanged
	{
		private Height _height;
		private string _label;
		private uint256 _spenderTransactionId;
		private bool _coinJoinInProgress;
		private DateTimeOffset? _bannedUntilUtc;

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
					var rememberConfirmed = Confirmed;

					_height = value;
					OnPropertyChanged(nameof(Height));

					if (rememberConfirmed != Confirmed)
					{
						OnPropertyChanged(nameof(Confirmed));
					}
				}
			}
		}

		[JsonProperty(Order = 6)]
		public string Label
		{
			get { return _label; }
			set
			{
				if (value != _label)
				{
					_label = value;
					OnPropertyChanged(nameof(Label));
				}
			}
		}

		[JsonProperty(Order = 7)]
		[JsonConverter(typeof(Uint256JsonConverter))]
		public uint256 SpenderTransactionId
		{
			get { return _spenderTransactionId; }
			set
			{
				if (value != _spenderTransactionId)
				{
					var rememberSpentOrCoinJoinInProgress = SpentOrCoinJoinInProgress;
					var rememberUnspent = Unspent;

					_spenderTransactionId = value;
					OnPropertyChanged(nameof(SpenderTransactionId));

					if (rememberUnspent != Unspent)
					{
						OnPropertyChanged(nameof(Unspent));
					}

					if (rememberSpentOrCoinJoinInProgress != SpentOrCoinJoinInProgress)
					{
						OnPropertyChanged(nameof(SpentOrCoinJoinInProgress));
					}
				}
			}
		}

		[JsonProperty(Order = 8)]
		public TxoRef[] SpentOutputs { get; }

		[JsonProperty(Order = 9)]
		[JsonConverter(typeof(FunnyBoolJsonConverter))]
		public bool RBF { get; }

		[JsonProperty(Order = 10)]
		[JsonConverter(typeof(FunnyBoolJsonConverter))]
		public bool CoinJoinInProgress
		{
			get => _coinJoinInProgress;
			set
			{
				if (_coinJoinInProgress != value)
				{
					var rememberSpentOrCoinJoinInProgress = SpentOrCoinJoinInProgress;

					_coinJoinInProgress = value;
					OnPropertyChanged(nameof(CoinJoinInProgress));

					if (rememberSpentOrCoinJoinInProgress != SpentOrCoinJoinInProgress)
					{
						OnPropertyChanged(nameof(SpentOrCoinJoinInProgress));
					}
				}
			}
		}

		/// <summary>
		/// AnonymitySet - 1
		/// </summary>
		[JsonProperty(Order = 11)]
		public int Mixin { get; }

		[JsonProperty(Order = 12)]
		public DateTimeOffset? BannedUntilUtc
		{
			get
			{
				return _bannedUntilUtc;
			}
			set
			{
				// ToDo: IsBanned doesn't get notified when it gets unbanned.
				if (_bannedUntilUtc != value)
				{
					var rememberIsBanned = IsBanned;
					_bannedUntilUtc = value;
					OnPropertyChanged(nameof(BannedUntilUtc));
					if (rememberIsBanned != IsBanned)
					{
						OnPropertyChanged(nameof(IsBanned));
					}
				}
			}
		}

		/// <summary>
		/// If the backend thinks it's spent, but Wasabi doesn't yet know.
		/// </summary>
		[JsonProperty(Order = 13)]
		public bool SpentAccordingToBackend { get; set; }

		public bool SpentOrCoinJoinInProgress => !(SpenderTransactionId is null) || CoinJoinInProgress || SpentAccordingToBackend;
		public bool Unspent => SpenderTransactionId is null && !SpentAccordingToBackend;
		public bool Confirmed => Height != Height.MemPool && Height != Height.Unknown;
		public bool IsBanned => BannedUntilUtc != null && BannedUntilUtc > DateTimeOffset.UtcNow;

		/// <summary>
		/// Mixin + 1
		/// </summary>
		public int AnonymitySet => Mixin + 1;

		/// <summary>
		/// It's a secret, so it's usually going to be null. Don't use it.
		/// </summary>
		public ISecret Secret { get; set; }

		[JsonConstructor]
		public SmartCoin(uint256 transactionId, uint index, Script scriptPubKey, Money amount, TxoRef[] spentOutputs, Height height, bool rbf, int mixin, string label = "", uint256 spenderTransactionId = null, bool coinJoinInProgress = false, bool spentAccordingToBackend = false)
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
			CoinJoinInProgress = coinJoinInProgress;
			Secret = null;
			BannedUntilUtc = null;
			SpentAccordingToBackend = spentAccordingToBackend;
		}

		public SmartCoin(Coin coin, TxoRef[] spentOutputs, Height height, bool rbf, int mixin, string label = "", uint256 spenderTransactionId = null, bool coinJoinInProgress = false, bool spentAccordingToBackend = false)
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
			CoinJoinInProgress = coinJoinInProgress;
			Secret = null;
			SpentAccordingToBackend = spentAccordingToBackend;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

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

		public bool HasLabel() => !string.IsNullOrWhiteSpace(Label);

		#region EqualityAndComparison

		public override bool Equals(object obj) => obj is SmartCoin && this == (SmartCoin)obj;

		public bool Equals(SmartCoin other) => this == other;

		public override int GetHashCode() => TransactionId.GetHashCode() ^ (int)Index;

		public static bool operator ==(SmartCoin x, SmartCoin y) => y?.TransactionId == x?.TransactionId && y?.Index == x?.Index;

		public static bool operator !=(SmartCoin x, SmartCoin y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
