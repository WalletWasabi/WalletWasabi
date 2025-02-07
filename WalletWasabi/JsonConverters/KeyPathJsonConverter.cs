using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters;

public class KeyPathJsonConverter : JsonConverter<KeyPath>
{
	/// <inheritdoc />
	public override KeyPath? ReadJson(JsonReader reader, Type objectType, KeyPath? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var s = (string?)reader.Value;
		if (string.IsNullOrWhiteSpace(s))
		{
			return null;
		}
		var kp = KeyPath.Parse(s.Trim());

		return kp;
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, KeyPath? value, JsonSerializer serializer)
	{
		var s = value?.ToString() ?? throw new ArgumentNullException(nameof(value));
		writer.WriteValue(s);
	}
}
