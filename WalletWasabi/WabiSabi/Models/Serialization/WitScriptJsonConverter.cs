using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Models.Serialization;

public class WitScriptJsonConverter : JsonConverter<WitScript>
{
	/// <inheritdoc />
	public override WitScript? ReadJson(JsonReader reader, Type objectType, WitScript? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.Value is string serialized)
		{
			return new WitScript(ByteHelpers.FromHex(serialized));
		}
		throw new ArgumentException($"No valid serialized {nameof(WitScript)} passed.");
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, WitScript? value, JsonSerializer serializer)
	{
		var bytes = value?.ToBytes() ?? throw new ArgumentNullException(nameof(value));
		writer.WriteValue(ByteHelpers.ToHex(bytes));
	}
}
