using NBitcoin.DataEncoders;
using Newtonsoft.Json;

namespace WalletWasabi.Affiliation.Serialization;

public class ByteArrayJsonConverter : JsonConverter<byte[]>
{
	public override byte[]? ReadJson(JsonReader reader, Type objectType, byte[]? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.Value is string serialized)
		{
			return Encoders.Hex.DecodeData(serialized);
		}
		else
		{
			throw new Exception();
		}
	}

	public override void WriteJson(JsonWriter writer, byte[]? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			throw new ArgumentNullException(nameof(value));
		}
		else
		{
			writer.WriteValue(Encoders.Hex.EncodeData(value));
		}
	}
}
