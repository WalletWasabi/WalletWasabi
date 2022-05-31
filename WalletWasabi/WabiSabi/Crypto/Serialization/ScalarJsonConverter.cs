using NBitcoin.Secp256k1;
using Newtonsoft.Json;

namespace WalletWasabi.WabiSabi.Crypto.Serialization;

public class ScalarJsonConverter : JsonConverter<Scalar>
{
	/// <inheritdoc />
	public override Scalar ReadJson(JsonReader reader, Type objectType, Scalar existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.Value is string serialized)
		{
			return new Scalar(ByteHelpers.FromHex(serialized));
		}
		throw new ArgumentException($"No valid serialized {nameof(Scalar)} passed.");
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, Scalar value, JsonSerializer serializer)
	{
		if (value is Scalar scalar)
		{
			writer.WriteValue(ByteHelpers.ToHex(scalar.ToBytes()));
			return;
		}
		throw new ArgumentException($"No valid {nameof(Scalar)}.", nameof(value));
	}
}
