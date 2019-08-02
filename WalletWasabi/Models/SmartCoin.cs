using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Models
{
	/// <summary>
	/// An UTXO that knows more.
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public class SmartCoin : NotifyPropertyChangedBase, IEquatable<SmartCoin>
	{
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
			set => RaiseAndSetIfChanged(ref _transactionId, value);
		}

		[JsonProperty]
		public uint Index
		{
			get => _index;
			set => RaiseAndSetIfChanged(ref _index, value);
		}

		[JsonProperty]
		[JsonConverter(typeof(ScriptJsonConverter))]
		public Script ScriptPubKey
		{
			get => _scriptPubKey;
			set => RaiseAndSetIfChanged(ref _scriptPubKey, value);
		}

		[JsonProperty]
		[JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money Amount
		{
			get => _amount;
			set => RaiseAndSetIfChanged(ref _amount, value);
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
			set => RaiseAndSetIfChanged(ref _spentOutputs, value);
		}

		[JsonProperty]
		[JsonConverter(typeof(FunnyBoolJsonConverter))]
		public bool IsReplaceable
		{
			get => _replaceable && !Confirmed;
			set => RaiseAndSetIfChanged(ref _replaceable, value);
		}

		[JsonProperty]
		public int AnonymitySet
		{
			get => _anonymitySet;
			private set => RaiseAndSetIfChanged(ref _anonymitySet, value);
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
			private set => RaiseAndSetIfChanged(ref _hdPubKey, value);
		}

		#endregion SerializableProperties

		#region NonSerializableProperties

		/// <summary>
		/// It's a secret, so it's usually going to be null. Do not use it.
		/// This will not get serialized, because that's a security risk.
		/// </summary>
		public ISecret Secret
		{
			get => _secret;
			set => RaiseAndSetIfChanged(ref _secret, value);
		}

		public string Clusters
		{
			get => _clusters;
			private set => RaiseAndSetIfChanged(ref _clusters, value);
		}

		#endregion NonSerializableProperties

		#region DependentProperties

		public bool Confirmed
		{
			get => _confirmed;
			private set => RaiseAndSetIfChanged(ref _confirmed, value);
		}

		/// <summary>
		/// Spent || SpentAccordingToBackend || CoinJoinInProgress || IsDust;
		/// </summary>
		public bool Unavailable
		{
			get => _unavailable;
			private set => RaiseAndSetIfChanged(ref _unavailable, value);
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
			private set => RaiseAndSetIfChanged(ref _isBanned, value);
		}

		#endregion DependentProperties

		#region PropertySetters

		private void SetConfirmed()
		{
			Confirmed = Height != Height.Mempool && Height != Height.Unknown;
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

		public override bool Equals(object obj) => obj is SmartCoin coin && this == coin;

		public bool Equals(SmartCoin other) => this == other;

		public override int GetHashCode() => TransactionId.GetHashCode() ^ (int)Index;

		public static bool operator ==(SmartCoin x, SmartCoin y) => y?.TransactionId == x?.TransactionId && y?.Index == x?.Index;

		public static bool operator !=(SmartCoin x, SmartCoin y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
