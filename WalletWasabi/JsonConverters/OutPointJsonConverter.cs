using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters;

public class OutPointJsonConverter : JsonConverter<OutPoint>
{
	/// <inheritdoc />
	public override OutPoint? ReadJson(JsonReader reader, Type objectType, OutPoint? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var value = (string?)reader.Value;
		var op = new OutPoint();
		op.FromHex(value);
		return op;
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, OutPoint? value, JsonSerializer serializer)
	{
		string opHex = value?.ToHex() ?? throw new ArgumentNullException(nameof(value));
		writer.WriteValue(opHex);
	}
}
