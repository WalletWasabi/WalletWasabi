using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters.Timing;

public class TimeSpanJsonConverterNg : JsonConverter<TimeSpan>
{
	/// <inheritdoc />
	public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.String)
		{
			return default;
		}

		string? stringValue = reader.GetString();
		return Parse(stringValue);
	}

	public static TimeSpan Parse(string? stringValue)
	{
		if (string.IsNullOrWhiteSpace(stringValue))
		{
			return default;
		}

		var daysParts = stringValue.Split('d');
		var days = int.Parse(daysParts[0].Trim());
		var hoursParts = daysParts[1].Split('h');
		var hours = int.Parse(hoursParts[0].Trim());
		var minutesParts = hoursParts[1].Split('m');
		var minutes = int.Parse(minutesParts[0].Trim());
		var secondsParts = minutesParts[1].Split('s');
		var seconds = int.Parse(secondsParts[0].Trim());
		return new TimeSpan(days, hours, minutes, seconds);
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
	{
		var ts = value;
		writer.WriteStringValue($"{ts.Days}d {ts.Hours}h {ts.Minutes}m {ts.Seconds}s");
	}
}
