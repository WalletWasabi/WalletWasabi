using NBitcoin;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Models
{
	/// <summary>
	/// An UTXO that knows more.
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public class SmartCoin : IEquatable<SmartCoin>, INotifyPropertyChanged
	{
		#region Events

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion Events

		#region Fields

		private uint256 _transactionId;
		private uint _index;
		private Script _scriptPubKey;
		private Money _amount;
		private Height _height;
		private string _label;
		private TxoRef[] _spentOutputs;
		private bool _replaceable;
		private int _anonymitySet;
		private uint256 _spenderTransactionId;
		private bool _coinJoinInProgress;
		private DateTimeOffset? _bannedUntilUtc;
		private bool _spentAccordingToBackend;
		private HdPubKey _hdPubKey;

		private ISecret _secret;
		private string _clusters;

		private bool _confirmed;
		private bool _unavailable;
		private bool _unspent;
		private bool _isBanned;

		#endregion Fields

		#region Properties

		#region SerializableProperties

		[JsonProperty]
		[JsonConverter(typeof(Uint256JsonConverter))]
		public uint256 TransactionId
		{
			get => _transactionId;
			set
			{
				if (value != _transactionId)
				{
					_transactionId = value;
					OnPropertyChanged(nameof(TransactionId));
				}
			}
		}

		[JsonProperty]
		public uint Index
		{
			get => _index;
			set
			{
				if (value != _index)
				{
					_index = value;
					OnPropertyChanged(nameof(Index));
				}
			}
		}

		[JsonProperty]
		[JsonConverter(typeof(ScriptJsonConverter))]
		public Script ScriptPubKey
		{
			get => _scriptPubKey;
			set
			{
				if (value != _scriptPubKey)
				{
					_scriptPubKey = value;
					OnPropertyChanged(nameof(ScriptPubKey));
				}
			}
		}

		[JsonProperty]
		[JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money Amount
		{
			get => _amount;
			set
			{
				if (value != _amount)
				{
					_amount = value;
					OnPropertyChanged(nameof(Amount));
				}
			}
		}

		[JsonProperty]
		[JsonConverter(typeof(HeightJsonConverter))]
		public Height Height
		{
			get => _height;
			set
			{
				if (value != _height)
				{
					_height = value;
					OnPropertyChanged(nameof(Height));
					SetConfirmed();
				}
			}
		}

		/// <summary>
		/// Always set it before the Amount!
		/// </summary>
		[JsonProperty]
		public string Label
		{
			get => _label;
			set
			{
				value = Guard.Correct(value);
				if (value != _label)
				{
					_label = value;
					OnPropertyChanged(nameof(Label));
					HasLabel = !string.IsNullOrEmpty(value);
				}
			}
		}

		[JsonProperty]
		public TxoRef[] SpentOutputs
		{
			get => _spentOutputs;
			set
			{
				if (value != _spentOutputs)
				{
					_spentOutputs = value;
					OnPropertyChanged(nameof(SpentOutputs));
				}
			}
		}

		[JsonProperty]
		[JsonConverter(typeof(FunnyBoolJsonConverter))]
		public bool IsReplaceable
		{
			get => _replaceable && !Confirmed;
			set
			{
				if (value != _replaceable)
				{
					_replaceable = value;
					OnPropertyChanged(nameof(IsReplaceable));
				}
			}
		}

		[JsonProperty]
		public int AnonymitySet
		{
			get => _anonymitySet;
			private set
			{
				if (value != _anonymitySet)
				{
					_anonymitySet = value;
					OnPropertyChanged(nameof(AnonymitySet));
				}
			}
		}

		[JsonProperty]
		[JsonConverter(typeof(Uint256JsonConverter))]
		public uint256 SpenderTransactionId
		{
			get => _spenderTransactionId;
			set
			{
				if (value != _spenderTransactionId)
				{
					_spenderTransactionId = value;
					OnPropertyChanged(nameof(SpenderTransactionId));

					SetUnspent();
				}
			}
		}

		[JsonProperty]
		[JsonConverter(typeof(FunnyBoolJsonConverter))]
		public bool CoinJoinInProgress
		{
			get => _coinJoinInProgress;
			set
			{
				if (_coinJoinInProgress != value)
				{
					_coinJoinInProgress = value;
					OnPropertyChanged(nameof(CoinJoinInProgress));

					SetUnavailable();
				}
			}
		}

		[JsonProperty]
		public DateTimeOffset? BannedUntilUtc
		{
			get => _bannedUntilUtc;
			set
			{
				// ToDo: IsBanned does not get notified when it gets unbanned.
				if (_bannedUntilUtc != value)
				{
					_bannedUntilUtc = value;
					OnPropertyChanged(nameof(BannedUntilUtc));
					SetIsBanned();
				}
			}
		}

		/// <summary>
		/// If the backend thinks it's spent, but Wasabi does not yet know.
		/// </summary>
		[JsonProperty]
		public bool SpentAccordingToBackend
		{
			get => _spentAccordingToBackend;
			set
			{
				if (value != _spentAccordingToBackend)
				{
					_spentAccordingToBackend = value;
					OnPropertyChanged(nameof(SpentAccordingToBackend));

					SetUnavailable();
				}
			}
		}

		[JsonProperty]
		public HdPubKey HdPubKey
		{
			get => _hdPubKey;
			private set
			{
				if (value != _hdPubKey)
				{
					_hdPubKey = value;
					OnPropertyChanged(nameof(HdPubKey));
				}
			}
		}

		#endregion SerializableProperties

		#region NonSerializableProperties

		/// <summary>
		/// It's a secret, so it's usually going to be null. Don't use it.
		/// This will not get serialized, because that's a security risk.
		/// </summary>
		public ISecret Secret
		{
			get => _secret;
			set
			{
				if (value != _secret)
				{
					_secret = value;
					OnPropertyChanged(nameof(Secret));
				}
			}
		}

		public string Clusters
		{
			get => _clusters;
			private set
			{
				if (value != _clusters)
				{
					_clusters = value;
					OnPropertyChanged(nameof(Clusters));
				}
			}
		}

		#endregion NonSerializableProperties

		#region DependentProperties

		public bool Confirmed
		{
			get => _confirmed;
			private set
			{
				if (value != _confirmed)
				{
					_confirmed = value;
					OnPropertyChanged(nameof(Confirmed));
				}
			}
		}

		/// <summary>
		/// Spent || SpentAccordingToBackend || CoinJoinInProgress || IsDust;
		/// </summary>
		public bool Unavailable
		{
			get => _unavailable;
			private set
			{
				if (value != _unavailable)
				{
					_unavailable = value;
					OnPropertyChanged(nameof(Unavailable));
				}
			}
		}

		public bool Unspent
		{
			get => _unspent;
			private set
			{
				if (value != _unspent)
				{
					_unspent = value;
					OnPropertyChanged(nameof(Unspent));

					SetUnavailable();
				}
			}
		}

		public bool IsBanned
		{
			get => _isBanned;
			private set
			{
				if (value != _isBanned)
				{
					_isBanned = value;
					OnPropertyChanged(nameof(IsBanned));
				}
			}
		}

		#endregion DependentProperties

		#region PropertySetters

		private void SetConfirmed()
		{
			Confirmed = Height != Height.MemPool && Height != Height.Unknown;
		}

		private void SetUnspent()
		{
			Unspent = SpenderTransactionId is null;
		}

		private void SetIsBanned()
		{
			IsBanned = BannedUntilUtc != null && BannedUntilUtc > DateTimeOffset.UtcNow;
		}

		private void SetUnavailable()
		{
			Unavailable = !Unspent || SpentAccordingToBackend || CoinJoinInProgress;
		}

		#endregion PropertySetters

		#endregion Properties

		#region Constructors

		[JsonConstructor]
		public SmartCoin(uint256 transactionId, uint index, Script scriptPubKey, Money amount, TxoRef[] spentOutputs, Height height, bool replaceable, int anonymitySet, string label = "", uint256 spenderTransactionId = null, bool coinJoinInProgress = false, DateTimeOffset? bannedUntilUtc = null, bool spentAccordingToBackend = false, HdPubKey pubKey = null)
		{
			Create(transactionId, index, scriptPubKey, amount, spentOutputs, height, replaceable, anonymitySet, label, spenderTransactionId, coinJoinInProgress, bannedUntilUtc, spentAccordingToBackend, pubKey);
		}

		public SmartCoin(Coin coin, TxoRef[] spentOutputs, Height height, bool replaceable, int anonymitySet, string label = "", uint256 spenderTransactionId = null, bool coinJoinInProgress = false, DateTimeOffset? bannedUntilUtc = null, bool spentAccordingToBackend = false, HdPubKey pubKey = null)
		{
			OutPoint outpoint = Guard.NotNull($"{coin}.{coin?.Outpoint}", coin?.Outpoint);
			uint256 transactionId = outpoint.Hash;
			uint index = outpoint.N;
			Script scriptPubKey = Guard.NotNull($"{coin}.{coin?.ScriptPubKey}", coin?.ScriptPubKey);
			Money amount = Guard.NotNull($"{coin}.{coin?.Amount}", coin?.Amount);

			Create(transactionId, index, scriptPubKey, amount, spentOutputs, height, replaceable, anonymitySet, label, spenderTransactionId, coinJoinInProgress, bannedUntilUtc, spentAccordingToBackend, pubKey);
		}

		private void Create(uint256 transactionId, uint index, Script scriptPubKey, Money amount, TxoRef[] spentOutputs, Height height, bool replaceable, int anonymitySet, string label, uint256 spenderTransactionId, bool coinJoinInProgress, DateTimeOffset? bannedUntilUtc, bool spentAccordingToBackend, HdPubKey pubKey)
		{
			TransactionId = Guard.NotNull(nameof(transactionId), transactionId);
			Index = Guard.NotNull(nameof(index), index);
			ScriptPubKey = Guard.NotNull(nameof(scriptPubKey), scriptPubKey);
			Amount = Guard.NotNull(nameof(amount), amount);
			Height = height;
			Label = Guard.Correct(label);
			SpentOutputs = Guard.NotNullOrEmpty(nameof(spentOutputs), spentOutputs);
			IsReplaceable = replaceable;
			AnonymitySet = Guard.InRangeAndNotNull(nameof(anonymitySet), anonymitySet, 1, int.MaxValue);

			SpenderTransactionId = spenderTransactionId;

			CoinJoinInProgress = coinJoinInProgress;
			BannedUntilUtc = bannedUntilUtc;
			SpentAccordingToBackend = spentAccordingToBackend;

			HdPubKey = pubKey;

			SetConfirmed();
			SetUnspent();
			SetIsBanned();
			SetUnavailable();
		}

		#endregion Constructors

		#region Methods

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

		public bool HasLabel { get; private set; }

		public void SetClusters(string clusters)
		{
			Clusters = clusters;
		}

		#endregion Methods

		#region EqualityAndComparison

		public override bool Equals(object obj) => obj is SmartCoin && this == (SmartCoin)obj;

		public bool Equals(SmartCoin other) => this == other;

		public override int GetHashCode() => TransactionId.GetHashCode() ^ (int)Index;

		public static bool operator ==(SmartCoin x, SmartCoin y) => y?.TransactionId == x?.TransactionId && y?.Index == x?.Index;

		public static bool operator !=(SmartCoin x, SmartCoin y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
