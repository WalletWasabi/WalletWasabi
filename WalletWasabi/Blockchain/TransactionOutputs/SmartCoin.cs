using NBitcoin;
using System;
using System.Linq;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
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

		private Height _height;
		private SmartLabel _label;
		private int _anonymitySet;
		private SmartTransaction? _spenderTransaction;
		private bool _coinJoinInProgress;
		private DateTimeOffset? _bannedUntilUtc;
		private bool _spentAccordingToBackend;

		private ISecret _secret;

		private Cluster _cluster;

		private bool _confirmed;
		private bool _isBanned;

		#endregion Fields

		#region Constructors

		public SmartCoin(SmartTransaction transaction, uint outputIndex, HdPubKey pubKey, int anonymitySet, SmartLabel label = null, bool coinJoinInProgress = false, DateTimeOffset? bannedUntilUtc = null, bool spentAccordingToBackend = false)
		{
			Transaction = transaction;
			Coin = new Coin(transaction.Transaction, outputIndex);
			HashCode = (TransactionId, Index).GetHashCode();
			Height = transaction.Height;
			SpentOutputs = Transaction.Transaction.Inputs.ToOutPoints().ToArray();
			WasReplaceable = Transaction.IsRBF;
			AnonymitySet = Guard.InRangeAndNotNull(nameof(anonymitySet), anonymitySet, 1, int.MaxValue);

			CoinJoinInProgress = coinJoinInProgress;
			BannedUntilUtc = bannedUntilUtc;
			SpentAccordingToBackend = spentAccordingToBackend;

			HdPubKey = pubKey;

			Label = SmartLabel.Merge(HdPubKey.Label, label);

			Cluster = new Cluster(this);

			SetIsBanned();
		}

		#endregion Constructors

		#region Properties

		public SmartTransaction Transaction { get; }

		public Coin Coin { get; }

		public Script ScriptPubKey => Coin.ScriptPubKey;

		public Money Amount => Coin.Amount;

		public OutPoint OutPoint => Coin.Outpoint;

		public uint256 TransactionId => OutPoint.Hash;

		public uint Index => OutPoint.N;
		private int HashCode { get; }

		public Height Height
		{
			get => _height;
			set
			{
				if (RaiseAndSetIfChanged(ref _height, value))
				{
					Confirmed = _height.Type == HeightType.Chain;
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

		public OutPoint[] SpentOutputs { get; }

		public bool WasReplaceable { get; }

		public int AnonymitySet
		{
			get => _anonymitySet;
			set => RaiseAndSetIfChanged(ref _anonymitySet, value);
		}

		public SmartTransaction? SpenderTransaction
		{
			get => _spenderTransaction;
			set => RaiseAndSetIfChanged(ref _spenderTransaction, value);
		}

		public bool CoinJoinInProgress
		{
			get => _coinJoinInProgress;
			set => RaiseAndSetIfChanged(ref _coinJoinInProgress, value);
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
			set => RaiseAndSetIfChanged(ref _spentAccordingToBackend, value);
		}

		public HdPubKey HdPubKey { get; }

		/// <summary>
		/// It's a secret, so it's usually going to be null. Do not use it.
		/// This will not get serialized, because that's a security risk.
		/// </summary>
		public ISecret Secret
		{
			get => _secret;
			set => RaiseAndSetIfChanged(ref _secret, value);
		}

		public Cluster Cluster
		{
			get => _cluster;
			set => RaiseAndSetIfChanged(ref _cluster, value);
		}

		#region DependentProperties

		public bool Confirmed
		{
			get => _confirmed;
			private set => RaiseAndSetIfChanged(ref _confirmed, value);
		}

		public bool IsBanned
		{
			get => _isBanned;
			private set => RaiseAndSetIfChanged(ref _isBanned, value);
		}

		#endregion DependentProperties

		#region PropertySetters

		public void SetIsBanned()
		{
			IsBanned = BannedUntilUtc is { } && BannedUntilUtc > DateTimeOffset.UtcNow;
		}

		#endregion PropertySetters

		#endregion Properties

		#region Methods

		public bool IsSpent() => SpenderTransaction is { };

		/// <summary>
		/// IsUnspent() AND !SpentAccordingToBackend AND !CoinJoinInProgress
		/// </summary>
		public bool IsAvailable() => SpenderTransaction is null && !SpentAccordingToBackend && !CoinJoinInProgress;

		public bool IsReplaceable() => WasReplaceable && !Confirmed;

		#endregion Methods

		#region EqualityAndComparison

		public override bool Equals(object? obj) => Equals(obj as SmartCoin);

		public bool Equals(SmartCoin? other) => this == other;

		public override int GetHashCode() => HashCode;

		public static bool operator ==(SmartCoin? x, SmartCoin? y)
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

		public static bool operator !=(SmartCoin? x, SmartCoin? y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
