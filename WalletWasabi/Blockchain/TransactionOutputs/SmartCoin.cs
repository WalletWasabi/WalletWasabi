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
		private Height _height;
		private int _anonymitySet;
		private SmartTransaction? _spenderTransaction;
		private bool _coinJoinInProgress;
		private DateTimeOffset? _bannedUntilUtc;
		private bool _spentAccordingToBackend;

		private ISecret? _secret;

		private Cluster _cluster;

		private bool _confirmed;
		private bool _isBanned;

		private Lazy<uint256> _transactionId;
		private Lazy<OutPoint> _outPoint;
		private Lazy<TxOut> _txOut;
		private Lazy<Coin> _coin;
		private Lazy<int> _hashCode;

		public SmartCoin(SmartTransaction transaction, uint outputIndex, HdPubKey pubKey, int anonymitySet)
		{
			Transaction = transaction;
			Index = outputIndex;
			_transactionId = new Lazy<uint256>(() => Transaction.GetHash(), true);

			_outPoint = new Lazy<OutPoint>(() => new OutPoint(TransactionId, Index), true);
			_txOut = new Lazy<TxOut>(() => Transaction.Transaction.Outputs[Index], true);
			_coin = new Lazy<Coin>(() => new Coin(OutPoint, TxOut), true);

			_hashCode = new Lazy<int>(() => OutPoint.GetHashCode(), true);

			Height = transaction.Height;
			AnonymitySet = Guard.InRangeAndNotNull(nameof(anonymitySet), anonymitySet, 1, int.MaxValue);

			HdPubKey = pubKey;

			_cluster = new Cluster(this);
			Transaction.WalletInputs.Add(this);
		}

		public SmartTransaction Transaction { get; }
		public uint Index { get; }
		public uint256 TransactionId => _transactionId.Value;

		public OutPoint OutPoint => _outPoint.Value;
		public TxOut TxOut => _txOut.Value;
		public Coin Coin => _coin.Value;

		public Script ScriptPubKey => TxOut.ScriptPubKey;
		public Money Amount => TxOut.Value;

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

		public int AnonymitySet
		{
			get => _anonymitySet;
			set => RaiseAndSetIfChanged(ref _anonymitySet, value);
		}

		public SmartTransaction? SpenderTransaction
		{
			get => _spenderTransaction;
			set
			{
				if (RaiseAndSetIfChanged(ref _spenderTransaction, value))
				{
					value?.WalletOutputs.Add(this);
				}
			}
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
		public ISecret? Secret
		{
			get => _secret;
			set => RaiseAndSetIfChanged(ref _secret, value);
		}

		public Cluster Cluster
		{
			get => _cluster;
			set => RaiseAndSetIfChanged(ref _cluster, value);
		}

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

		public void SetIsBanned()
		{
			IsBanned = BannedUntilUtc is { } && BannedUntilUtc > DateTimeOffset.UtcNow;
		}

		public bool IsSpent() => SpenderTransaction is { };

		/// <summary>
		/// IsUnspent() AND !SpentAccordingToBackend AND !CoinJoinInProgress
		/// </summary>
		public bool IsAvailable() => SpenderTransaction is null && !SpentAccordingToBackend && !CoinJoinInProgress;

		public bool IsReplaceable() => Transaction.IsRBF;

		#region EqualityAndComparison

		public override bool Equals(object? obj) => Equals(obj as SmartCoin);

		public bool Equals(SmartCoin? other) => this == other;

		public override int GetHashCode() => _hashCode.Value;

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
				var hashEquals = x.GetHashCode() == y.GetHashCode();
				return hashEquals && y.TransactionId == x.TransactionId && y.Index == x.Index;
			}
		}

		public static bool operator !=(SmartCoin? x, SmartCoin? y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
