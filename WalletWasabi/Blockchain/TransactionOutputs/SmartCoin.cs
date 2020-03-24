using NBitcoin;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.TransactionOutputs
{
	/// <summary>
	/// An UTXO that knows more.
	/// </summary>
	public class SmartCoin : NotifyPropertyChangedBase, IEquatable<SmartCoin>
	{
		#region Fields

		private Script _scriptPubKey;
		private Money _amount;
		private Height _height;
		private SmartLabel _label;
		private OutPoint[] _spentOutputs;
		private bool _replaceable;
		private int _anonymitySet;
		private uint256 _spenderTransactionId;
		private bool _coinJoinInProgress;
		private DateTimeOffset? _bannedUntilUtc;
		private bool _spentAccordingToBackend;
		private HdPubKey _hdPubKey;

		private ISecret _secret;

		private Cluster _clusters;

		private bool _confirmed;
		private bool _unavailable;
		private bool _unspent;
		private bool _isBanned;

		#endregion Fields

		#region Constructors

		public SmartCoin(uint256 transactionId, uint index, Script scriptPubKey, Money amount, OutPoint[] spentOutputs, Height height, bool replaceable, int anonymitySet, SmartLabel label = null, uint256 spenderTransactionId = null, bool coinJoinInProgress = false, DateTimeOffset? bannedUntilUtc = null, bool spentAccordingToBackend = false, HdPubKey pubKey = null)
		{
			Create(transactionId, index, scriptPubKey, amount, spentOutputs, height, replaceable, anonymitySet, label, spenderTransactionId, coinJoinInProgress, bannedUntilUtc, spentAccordingToBackend, pubKey);
		}

		public SmartCoin(Coin coin, OutPoint[] spentOutputs, Height height, bool replaceable, int anonymitySet, SmartLabel label = null, uint256 spenderTransactionId = null, bool coinJoinInProgress = false, DateTimeOffset? bannedUntilUtc = null, bool spentAccordingToBackend = false, HdPubKey pubKey = null)
		{
			OutPoint outpoint = Guard.NotNull($"{coin}.{coin?.Outpoint}", coin?.Outpoint);
			uint256 transactionId = outpoint.Hash;
			uint index = outpoint.N;
			Script scriptPubKey = Guard.NotNull($"{coin}.{coin?.ScriptPubKey}", coin?.ScriptPubKey);
			Money amount = Guard.NotNull($"{coin}.{coin?.Amount}", coin?.Amount);

			Create(transactionId, index, scriptPubKey, amount, spentOutputs, height, replaceable, anonymitySet, label, spenderTransactionId, coinJoinInProgress, bannedUntilUtc, spentAccordingToBackend, pubKey);
		}

		#endregion Constructors

		#region Properties

		public uint256 TransactionId => OutPoint.Hash;

		public uint Index => OutPoint.N;
		private int HashCode { get; set; }

		public OutPoint OutPoint { get; private set; }

		public Script ScriptPubKey
		{
			get => _scriptPubKey;
			private set => RaiseAndSetIfChanged(ref _scriptPubKey, value);
		}

		public Money Amount
		{
			get => _amount;
			private set => RaiseAndSetIfChanged(ref _amount, value);
		}

		public Height Height
		{
			get => _height;
			set
			{
				if (RaiseAndSetIfChanged(ref _height, value))
				{
					SetConfirmed();
				}
			}
		}

		/// <summary>
		/// Always set it before the Amount!
		/// </summary>
		public SmartLabel Label
		{
			get => _label;
			set => RaiseAndSetIfChanged(ref _label, value);
		}

		public OutPoint[] SpentOutputs
		{
			get => _spentOutputs;
			private set => RaiseAndSetIfChanged(ref _spentOutputs, value);
		}

		public bool IsReplaceable
		{
			get => _replaceable && !Confirmed;
			private set => RaiseAndSetIfChanged(ref _replaceable, value);
		}

		public int AnonymitySet
		{
			get => _anonymitySet;
			private set => RaiseAndSetIfChanged(ref _anonymitySet, value);
		}

		public uint256 SpenderTransactionId
		{
			get => _spenderTransactionId;
			set
			{
				if (RaiseAndSetIfChanged(ref _spenderTransactionId, value))
				{
					SetUnspent();
				}
			}
		}

		public bool CoinJoinInProgress
		{
			get => _coinJoinInProgress;
			set
			{
				if (RaiseAndSetIfChanged(ref _coinJoinInProgress, value))
				{
					SetUnavailable();
				}
			}
		}

		public DateTimeOffset? BannedUntilUtc
		{
			get => _bannedUntilUtc;
			set
			{
				// ToDo: IsBanned does not get notified when it gets unbanned.
				if (RaiseAndSetIfChanged(ref _bannedUntilUtc, value))
				{
					SetIsBanned();
				}
			}
		}

		/// <summary>
		/// If the backend thinks it's spent, but Wasabi does not yet know.
		/// </summary>
		public bool SpentAccordingToBackend
		{
			get => _spentAccordingToBackend;
			set
			{
				if (RaiseAndSetIfChanged(ref _spentAccordingToBackend, value))
				{
					SetUnavailable();
				}
			}
		}

		public HdPubKey HdPubKey
		{
			get => _hdPubKey;
			private set => RaiseAndSetIfChanged(ref _hdPubKey, value);
		}

		public bool? IsLikelyCoinJoinOutput { get; set; }

		/// <summary>
		/// It's a secret, so it's usually going to be null. Do not use it.
		/// This will not get serialized, because that's a security risk.
		/// </summary>
		public ISecret Secret
		{
			get => _secret;
			set => RaiseAndSetIfChanged(ref _secret, value);
		}

		public Cluster Clusters
		{
			get => _clusters;
			set => RaiseAndSetIfChanged(ref _clusters, value);
		}

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
				if (RaiseAndSetIfChanged(ref _unspent, value))
				{
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

		public void SetIsBanned()
		{
			IsBanned = BannedUntilUtc is { } && BannedUntilUtc > DateTimeOffset.UtcNow;
		}

		private void SetUnavailable()
		{
			Unavailable = !Unspent || SpentAccordingToBackend || CoinJoinInProgress;
		}

		#endregion PropertySetters

		#endregion Properties

		#region Methods

		private void Create(uint256 transactionId, uint index, Script scriptPubKey, Money amount, OutPoint[] spentOutputs, Height height, bool replaceable, int anonymitySet, SmartLabel label, uint256 spenderTransactionId, bool coinJoinInProgress, DateTimeOffset? bannedUntilUtc, bool spentAccordingToBackend, HdPubKey pubKey)
		{
			Guard.NotNull(nameof(transactionId), transactionId);
			Guard.NotNull(nameof(index), index);
			OutPoint = new OutPoint(transactionId, index);
			HashCode = (TransactionId, Index).GetHashCode();
			ScriptPubKey = Guard.NotNull(nameof(scriptPubKey), scriptPubKey);
			Amount = Guard.NotNull(nameof(amount), amount);
			Height = height;
			SpentOutputs = Guard.NotNullOrEmpty(nameof(spentOutputs), spentOutputs);
			IsReplaceable = replaceable;
			AnonymitySet = Guard.InRangeAndNotNull(nameof(anonymitySet), anonymitySet, 1, int.MaxValue);
			IsLikelyCoinJoinOutput = null;

			SpenderTransactionId = spenderTransactionId;

			CoinJoinInProgress = coinJoinInProgress;
			BannedUntilUtc = bannedUntilUtc;
			SpentAccordingToBackend = spentAccordingToBackend;

			HdPubKey = pubKey;

			Label = SmartLabel.Merge(HdPubKey?.Label, label);

			Clusters = new Cluster(this);

			SetConfirmed();
			SetUnspent();
			SetIsBanned();
			SetUnavailable();
		}

		public Coin GetCoin()
		{
			return new Coin(TransactionId, Index, Amount, ScriptPubKey);
		}

		#endregion Methods

		#region EqualityAndComparison

		public override bool Equals(object obj) => Equals(obj as SmartCoin);

		public bool Equals(SmartCoin other) => this == other;

		public override int GetHashCode() => HashCode;

		public static bool operator ==(SmartCoin x, SmartCoin y)
		{
			if (ReferenceEquals(x, y))
			{
				return true;
			}
			else if (x is null || y is null)
			{
				return false;
			}
			else
			{
				var hashEquals = x.HashCode == y.HashCode;
				return hashEquals && y.TransactionId == x.TransactionId && y.Index == x.Index;
			}
		}

		public static bool operator !=(SmartCoin x, SmartCoin y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
