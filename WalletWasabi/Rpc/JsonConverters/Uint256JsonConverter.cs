using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.Rpc.JsonConverters;

public class Uint256JsonConverter : JsonConverter<uint256>
{
	/// <inheritdoc />
	public override uint256? ReadJson(JsonReader reader, Type objectType, uint256? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		string? value = (string?)reader.Value;
		return value is null ? default : new uint256(value);
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, uint256? value, JsonSerializer serializer)
	{
		writer.WriteValue(value?.ToString());
	}
}
