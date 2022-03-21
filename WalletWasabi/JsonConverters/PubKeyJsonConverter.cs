using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters;

public class PubKeyJsonConverter : JsonConverter<PubKey>
{
	/// <inheritdoc />
	public override PubKey? ReadJson(JsonReader reader, Type objectType, PubKey? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		return new PubKey(((string?)reader.Value)?.Trim());
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, PubKey? value, JsonSerializer serializer)
	{
		var pubKeyHex = value?.ToHex() ?? throw new ArgumentNullException(nameof(value));
		writer.WriteValue(pubKeyHex);
	}
}
