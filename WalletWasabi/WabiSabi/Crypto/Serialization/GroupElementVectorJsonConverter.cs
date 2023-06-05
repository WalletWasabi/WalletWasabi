using Newtonsoft.Json;
using WabiSabi.Crypto.Groups;
using WalletWasabi.JsonConverters;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace WalletWasabi.WabiSabi.Crypto.Serialization;

public class GroupElementVectorJsonConverter : JsonConverter<GroupElementVector>
{
	/// <inheritdoc />
	public override GroupElementVector? ReadJson(JsonReader reader, Type objectType, GroupElementVector? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.TokenType == JsonToken.StartArray)
		{
			var ges = serializer.Deserialize<GroupElement[]>(reader)
				?? throw new JsonException("Array was expected. Null was given.");

			return ReflectionUtils.CreateInstance<GroupElementVector>(ges);
		}

		throw new ArgumentException($"No valid serialized {nameof(GroupElement)} passed.");
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, GroupElementVector? value, JsonSerializer serializer)
	{
		if (value is { } ges)
		{
			writer.WriteStartArray();
			foreach (var ge in value)
			{
				serializer.Serialize(writer, ge);
			}
			writer.WriteEndArray();
			return;
		}
		throw new ArgumentException($"No valid {nameof(GroupElementVector)}.", nameof(value));
	}
}
