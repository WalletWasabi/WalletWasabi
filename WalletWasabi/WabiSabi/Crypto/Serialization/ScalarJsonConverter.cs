using NBitcoin.Secp256k1;
using Newtonsoft.Json;

namespace WalletWasabi.WabiSabi.Crypto.Serialization;

public class ScalarJsonConverter : JsonConverter
{
	public override bool CanConvert(Type objectType)
	{
		return objectType == typeof(Scalar);
	}

	public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
	{
		if (reader.Value is string serialized)
		{
			return new Scalar(ByteHelpers.FromHex(serialized));
		}
		throw new ArgumentException($"No valid serialized {nameof(Scalar)} passed.");
	}

	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	{
		if (value is Scalar scalar)
		{
			writer.WriteValue(ByteHelpers.ToHex(scalar.ToBytes()));
			return;
		}
		throw new ArgumentException($"No valid {nameof(Scalar)}.", nameof(value));
	}
}
