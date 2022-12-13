using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using WalletWasabi.Helpers;

namespace WalletWasabi.Affiliation.Serialization;

public class ByteArrayJsonConverter : JsonConverter<byte[]>
{
	public override byte[]? ReadJson(JsonReader reader, Type objectType, byte[]? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.Value is string serialized)
		{
			return Encoders.Hex.DecodeData(serialized);
		}
		throw new JsonSerializationException("Cannot deserialize object.");
	}

	public override void WriteJson(JsonWriter writer, byte[]? value, JsonSerializer serializer)
	{
		Guard.NotNull(nameof(value), value);
		writer.WriteValue(Encoders.Hex.EncodeData(value));
	}
}
