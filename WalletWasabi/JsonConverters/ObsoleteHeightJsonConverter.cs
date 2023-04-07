using Newtonsoft.Json;
using WalletWasabi.Models;

namespace WalletWasabi.JsonConverters;

public class ObsoleteHeightJsonConverter : JsonConverter<Height>
{
	/// <inheritdoc />
	public override Height ReadJson(JsonReader reader, Type objectType, Height existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var stringValue = reader.Value?.ToString() ?? throw new InvalidOperationException("json reader returns null.");
		var value = long.Parse(stringValue);
		return value == 0 ? Height.Unknown : new Height((int)value);
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, Height value, JsonSerializer serializer)
	{
		var toWriteValue = value == Height.Unknown ? 0 : value.Value;
		writer.WriteValue(toWriteValue.ToString());
	}
}
