using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters;

public class DateTimeOffsetUnixSecondsConverter : JsonConverter<DateTimeOffset>
{
	/// <inheritdoc />
	public override DateTimeOffset ReadJson(JsonReader reader, Type objectType, DateTimeOffset existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var stringValue = reader.Value as string;
		if (string.IsNullOrWhiteSpace(stringValue))
		{
			return default;
		}
		else
		{
			return DateTimeOffset.FromUnixTimeSeconds(long.Parse(stringValue));
		}
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, DateTimeOffset value, JsonSerializer serializer)
	{
		writer.WriteValue(value.ToUnixTimeSeconds());
	}
}
