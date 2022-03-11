using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters;

public class KeyPathJsonConverter : JsonConverter
{
	/// <inheritdoc />
	public override bool CanConvert(Type objectType)
	{
		return objectType == typeof(KeyPath);
	}

	/// <inheritdoc />
	public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
	{
		if (reader.Value is null)
		{
			throw new ArgumentNullException("Can't read json, value was null.");
		}
		var s = (string)reader.Value;
		if (string.IsNullOrWhiteSpace(s))
		{
			return null;
		}
		var kp = KeyPath.Parse(s.Trim());

		return kp;
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			throw new ArgumentNullException("Can't write to json, value was null.");
		}
		var kp = (KeyPath)value;

		var s = kp.ToString();
		writer.WriteValue(s);
	}
}
