using Newtonsoft.Json;
using WalletWasabi.Models;

namespace WalletWasabi.JsonConverters;

public class HeightJsonConverter : JsonConverter<Height?>
{
	/// <inheritdoc />
	public override Height? ReadJson(JsonReader reader, Type objectType, Height? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var stringValue = reader.Value?.ToString() ?? null;
		if (stringValue is null)
		{
			return null;
		}
		var value = long.Parse(stringValue);
		return new Height((int)value);
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, Height? height, JsonSerializer serializer)
	{
		if (height is null)
		{
			writer.WriteNull();
		}
		else
		{
			writer.WriteValue(height.Value.Value.ToString());
		}
	}
}
