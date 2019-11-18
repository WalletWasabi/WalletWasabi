using NBitcoin;
using System;
using System.ComponentModel.DataAnnotations;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Blockchain.TransactionOutputs
{
	/// <summary>
	/// The same functionality as Outpoint, but it's JsonSerializable
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public class TxoRef : IEquatable<TxoRef>, IEquatable<OutPoint>
	{
		[Required]
		[JsonProperty(Order = 1)]
		[JsonConverter(typeof(Uint256JsonConverter))]
		public uint256 TransactionId { get; }

		[JsonProperty(Order = 2)]
		public uint Index { get; }

		[JsonConstructor]
		public TxoRef(uint256 transactionId, uint index)
		{
			TransactionId = Guard.NotNull(nameof(transactionId), transactionId);
			Index = Guard.NotNull(nameof(index), index);
		}

		public TxoRef(OutPoint outPoint)
		{
			Guard.NotNull(nameof(outPoint), outPoint);
			TransactionId = outPoint.Hash;
			Index = outPoint.N;
		}

		public OutPoint ToOutPoint() => new OutPoint(TransactionId, Index);

		#region EqualityAndComparison

		public override bool Equals(object obj) => obj is TxoRef txoRef && this == txoRef;

		public bool Equals(TxoRef other) => this == other;

		public override int GetHashCode() => TransactionId.GetHashCode() ^ (int)Index;

		public static bool operator ==(TxoRef x, TxoRef y) => y?.TransactionId == x?.TransactionId && y?.Index == x?.Index;

		public static bool operator !=(TxoRef x, TxoRef y) => !(x == y);

		public bool Equals(OutPoint other) => TransactionId == other?.Hash && Index == other?.N;

		public static bool operator ==(OutPoint x, TxoRef y) => y?.TransactionId == x?.Hash && y?.Index == x?.N;

		public static bool operator ==(TxoRef x, OutPoint y) => y?.Hash == x?.TransactionId && y?.N == x?.Index; // Hash first, index second is faster.

		public static bool operator !=(OutPoint x, TxoRef y) => !(x == y);

		public static bool operator !=(TxoRef x, OutPoint y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
