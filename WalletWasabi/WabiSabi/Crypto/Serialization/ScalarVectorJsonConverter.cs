using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using System.Linq;
using WabiSabi.Crypto;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.WabiSabi.Crypto.Serialization;

public class ScalarVectorJsonConverter : JsonConverter<ScalarVector>
{
	/// <inheritdoc />
	public override ScalarVector ReadJson(JsonReader reader, Type objectType, ScalarVector? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.TokenType == JsonToken.StartArray)
		{
			var scalars = serializer.Deserialize<Scalar[]>(reader)
				?? throw new ArgumentException("Scalars cannot be null.");

			return ReflectionUtils.CreateInstance<ScalarVector>(scalars.Cast<object>().ToArray())!;
		}
		throw new ArgumentException($"No valid serialized {nameof(ScalarVector)} passed.");
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, ScalarVector? value, JsonSerializer serializer)
	{
		if (value is { } scalars)
		{
			writer.WriteStartArray();
			foreach (var scalar in scalars)
			{
				serializer.Serialize(writer, scalar);
			}
			writer.WriteEndArray();
			return;
		}
		throw new ArgumentException($"No valid {nameof(ScalarVector)}.", nameof(value));
	}
}
