using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters.Timing;

public class TimeSpanJsonConverter : JsonConverter
{
	/// <inheritdoc />
	public override bool CanConvert(Type objectType)
	{
		return objectType == typeof(TimeSpan);
	}

	/// <inheritdoc />
	public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
	{
		var stringValue = reader.Value as string;
		return Parse(stringValue);
	}

	public static TimeSpan Parse(string? stringValue)
	{
		if (string.IsNullOrWhiteSpace(stringValue))
		{
			return default;
		}
		else
		{
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
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	{
		var ts = (TimeSpan)value;
		writer.WriteValue($"{ts.Days}d {ts.Hours}h {ts.Minutes}m {ts.Seconds}s");
	}
}
