using Newtonsoft.Json;
using WalletWasabi.Models;

namespace WalletWasabi.JsonConverters;

public class HeightJsonConverter : JsonConverter
{
	/// <inheritdoc />
	public override bool CanConvert(Type objectType)
	{
		return objectType == typeof(Height);
	}

	/// <inheritdoc />
	public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
	{
		var stringValue = reader.Value?.ToString() ?? throw new InvalidOperationException("json reader returns null.");
		var value = long.Parse(stringValue);
		return new Height((int)value);
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
	{
		writer.WriteValue(((Height)value).Value.ToString());
	}
}
