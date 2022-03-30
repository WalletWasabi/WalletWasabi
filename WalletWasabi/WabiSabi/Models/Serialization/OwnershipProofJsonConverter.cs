using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Crypto;

namespace WalletWasabi.WabiSabi.Models.Serialization;

public class OwnershipProofJsonConverter : JsonConverter
{
	public override bool CanConvert(Type objectType)
	{
		return objectType == typeof(OwnershipProof);
	}

	public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
	{
		var value = (string)reader.Value;
		return OwnershipProof.FromBytes(ByteHelpers.FromHex(value));
	}

	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	{
		var bytes = ((OwnershipProof)value).ToBytes();
		writer.WriteValue(ByteHelpers.ToHex(bytes));
	}
}
