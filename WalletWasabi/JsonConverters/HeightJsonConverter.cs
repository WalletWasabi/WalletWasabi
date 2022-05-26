using Newtonsoft.Json;
using WalletWasabi.Models;

namespace WalletWasabi.JsonConverters;

public class HeightJsonConverter : JsonConverter<Height>
{
	/// <inheritdoc />
	public override Height ReadJson(JsonReader reader, Type objectType, Height existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var stringValue = reader.Value?.ToString() ?? throw new InvalidOperationException("json reader returns null.");
		var value = long.Parse(stringValue);
		return new Height((int)value);
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, Height value, JsonSerializer serializer)
	{
		writer.WriteValue(value.Value.ToString());
	}
}
