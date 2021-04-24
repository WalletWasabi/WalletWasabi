using NBitcoin;
using Newtonsoft.Json;
using System;
using WalletWasabi.Crypto;

namespace WalletWasabi.JsonConverters
{
	public class OwnershipProofJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(OwnershipProof);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var value = (string)reader.Value;
			return OwnershipProof.FromBytes(ByteHelpers.FromHex(value));
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var bytes = ((OwnershipProof)value).ToBytes();
			writer.WriteValue(ByteHelpers.ToHex(bytes));
		}
	}
}
