using System;
using System.ComponentModel.DataAnnotations;
using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.JsonConverters;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class OutPointAsTxoRefJsonConverterTests
	{
		[Fact]
		public void Tests()
		{
			var outpoint = new OutPoint(new uint256("2716e680f47d74c1bc6f031da22331564dd4c6641d7216576aad1b846c85d492"), 765);
			var txoref   = new TxoRef(new uint256("2716e680f47d74c1bc6f031da22331564dd4c6641d7216576aad1b846c85d492"), 765);

			var oldConverters =new JsonConverter[]
			{
				new Uint256JsonConverter(),
				new OutPointJsonConverter(),
			};
			var newConverters = new JsonConverter[]
			{
				new Uint256JsonConverter(),
				new OutPointAsTxoRefJsonConverter(),
			};

			// Serialization test
			var serializedTxoRef = JsonConvert.SerializeObject(txoref, oldConverters);
			var serializedOutPoint = JsonConvert.SerializeObject(outpoint, newConverters);
			Assert.Equal(serializedTxoRef, serializedOutPoint);

			// Deserialization test
			var deserializedTxoRef = JsonConvert.DeserializeObject<TxoRef>(serializedTxoRef, oldConverters);
			var deserializedOutPoint = JsonConvert.DeserializeObject<TxoRef>(serializedTxoRef, newConverters);
			Assert.Equal(deserializedTxoRef, deserializedOutPoint);

			// Deserialization compatibility test
			deserializedTxoRef = JsonConvert.DeserializeObject<TxoRef>(serializedTxoRef, newConverters);
			deserializedOutPoint = JsonConvert.DeserializeObject<TxoRef>(serializedTxoRef, oldConverters);
			Assert.Equal(deserializedTxoRef, deserializedOutPoint);

			// Deserialization case insensitivity test
			var deserializedOutPoint2 = JsonConvert.DeserializeObject<TxoRef>(serializedOutPoint.ToLower(), newConverters);
			Assert.Equal(deserializedOutPoint, deserializedOutPoint2);
		}
	}


	[JsonObject(MemberSerialization.OptIn)]
	class TxoRef
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
			TransactionId = transactionId;
			Index = index;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj))
			{
				return true;
			}

			var other = obj as TxoRef;
			if (this is null || other is null)
			{
				return false;
			}
			return (TransactionId, Index) == (other.TransactionId, other.Index);
		}

		public override int GetHashCode() => (TransactionId, Index).GetHashCode();
	}
}