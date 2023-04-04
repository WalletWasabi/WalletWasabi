using System.Collections.Generic;
using System.Text.Json;
using Newtonsoft.Json;
using WabiSabi.Crypto.Groups;
using WalletWasabi.Helpers;
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
			var ges = serializer.Deserialize<GroupElement[]>(reader);
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
