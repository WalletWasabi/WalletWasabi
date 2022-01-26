using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters.Bitcoin;

public class ScriptJsonConverter : JsonConverter<Script>
{
	/// <inheritdoc />
	public override Script? ReadJson(JsonReader reader, Type objectType, Script? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var stringValue = reader.Value as string;
		return Parse(stringValue);
	}

	public static Script Parse(string? stringValue)
	{
		return new Script(stringValue);
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, Script? value, JsonSerializer serializer)
	{
		var stringValue = value?.ToString() ?? throw new ArgumentNullException(nameof(value));
		writer.WriteValue(stringValue);
	}
}
