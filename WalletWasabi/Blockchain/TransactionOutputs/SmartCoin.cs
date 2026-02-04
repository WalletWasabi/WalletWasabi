using NBitcoin;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.TransactionOutputs;

/// <summary>
/// An UTXO that knows more.
/// </summary>
[DebuggerDisplay("{Amount}BTC {Confirmed} {HdPubKey.Labels} OutPoint={Coin.Outpoint}")]
public class SmartCoin : NotifyPropertyChangedBase, IEquatable<SmartCoin>, IDestination, ISmartCoin
{
	private Height _height;
	private bool _confirmed;

	public SmartCoin(SmartTransaction transaction, uint outputIndex, HdPubKey pubKey)
	{
		Transaction = transaction;
		Coin = new Coin(Transaction.Transaction, outputIndex);

		_height = transaction.Height;
		_confirmed = _height.Type == HeightType.Chain;

		HdPubKey = pubKey;

		Transaction.TryAddWalletOutput(this);
	}

	public SmartTransaction Transaction { get; }
	public uint Index => Coin.Outpoint.N;
	public uint256 TransactionId => Coin.Outpoint.Hash;
	public OutPoint Outpoint => Coin.Outpoint;
	public TxOut TxOut => Coin.TxOut;
	public Coin Coin { get; }

	public Script ScriptPubKey => TxOut.ScriptPubKey;
	public ScriptType ScriptType => ScriptPubKey.GetScriptType();
	public Money Amount => TxOut.Value;
	public double AnonymitySet => HdPubKey.AnonymitySet;

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

	public SmartTransaction? SpenderTransaction
	{
		get;
		set => RaiseAndSetIfChanged(ref field, value);
	}

	public bool CoinJoinInProgress
	{
		get;
		set => RaiseAndSetIfChanged(ref field, value);
	}

	public DateTimeOffset? BannedUntilUtc
	{
		get;
		set => RaiseAndSetIfChanged(ref field, value);
	}

	public HdPubKey HdPubKey { get; }

	public bool Confirmed
	{
		get => _confirmed;
		private set => RaiseAndSetIfChanged(ref _confirmed, value);
	}

	public bool IsBanned => BannedUntilUtc is not null && BannedUntilUtc > DateTimeOffset.UtcNow;

	public bool IsExcludedFromCoinJoin
	{
		get;
		set => RaiseAndSetIfChanged(ref field, value);
	}

	/// <returns>False if external, or the tx inputs are all external.</returns>
	/// <remarks>Context: https://github.com/WalletWasabi/WalletWasabi/issues/10567</remarks>
	public bool IsSufficientlyDistancedFromExternalKeys { get; set; } = true;

	[MemberNotNullWhen(returnValue: true, nameof(SpenderTransaction))]
	public bool IsSpent() => SpenderTransaction is not null;

	/// <summary>
	/// IsUnspent() AND !SpentAccordingToBackend AND !CoinJoinInProgress
	/// </summary>
	public bool IsAvailable() => SpenderTransaction is null && !CoinJoinInProgress;

	public override string ToString() => $"{TransactionId.ToString()[..7]}.. - {Index}, {ScriptPubKey.ToString()[..7]}.. - {Amount} BTC";

	#region EqualityAndComparison

	public override bool Equals(object? obj) => Equals(obj as SmartCoin);

	public bool Equals(SmartCoin? other) => this == other;

	public override int GetHashCode() => Outpoint.GetHashCode();

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

		return y.Outpoint == x.Outpoint;
	}

	public static bool operator !=(SmartCoin? x, SmartCoin? y) => !(x == y);

	#endregion EqualityAndComparison
}
