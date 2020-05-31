using System.ComponentModel.DataAnnotations;
using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Tests.UnitTests.Converters
{
	[JsonObject(MemberSerialization.OptIn)]
	public class TxoRef
	{
		[JsonConstructor]
		public TxoRef(uint256 transactionId, uint index)
		{
			TransactionId = transactionId;
			Index = index;
		}

		[Required]
		[JsonProperty(Order = 1)]
		[JsonConverter(typeof(Uint256JsonConverter))]
		public uint256 TransactionId { get; }

		[JsonProperty(Order = 2)]
		public uint Index { get; }

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj))
			{
				return true;
			}

			if (this is null || !(obj is TxoRef other))
			{
				return false;
			}
			return (TransactionId, Index) == (other.TransactionId, other.Index);
		}

		public override int GetHashCode() => (TransactionId, Index).GetHashCode();
	}
}
