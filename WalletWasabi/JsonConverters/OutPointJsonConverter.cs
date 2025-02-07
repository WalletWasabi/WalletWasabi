using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Extensions;

namespace WalletWasabi.JsonConverters;

public class OutPointJsonConverter : JsonConverter<OutPoint>
{
	/// <inheritdoc />
	public override OutPoint? ReadJson(JsonReader reader, Type objectType, OutPoint? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.Value is string serialized)
		{
			var op = new OutPoint();
			op.FromHex(serialized);
			return op;
		}
		throw new ArgumentException($"No valid serialized {nameof(OutPoint)} passed.");
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, OutPoint? value, JsonSerializer serializer)
	{
		string opHex = value?.ToHex() ?? throw new ArgumentNullException(nameof(value));
		writer.WriteValue(opHex);
	}
}
