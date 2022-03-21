using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WalletWasabi.JsonConverters;

public class OutPointAsTxoRefJsonConverter : JsonConverter<OutPoint>
{
	/// <inheritdoc />
	public override OutPoint? ReadJson(JsonReader reader, Type objectType, OutPoint? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		JObject item = JObject.Load(reader);

		var hash = item.GetValue("TransactionId", StringComparison.OrdinalIgnoreCase).Value<string>();
		var n = item.GetValue("Index", StringComparison.OrdinalIgnoreCase).Value<uint>();
		return new OutPoint(uint256.Parse(hash), n);
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, OutPoint? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			throw new ArgumentNullException(nameof(value));
		}

		var outpoint = value;
		writer.WriteStartObject();
		writer.WritePropertyName("TransactionId");
		writer.WriteValue(outpoint.Hash.ToString());
		writer.WritePropertyName("Index");
		writer.WriteValue(outpoint.N);
		writer.WriteEndObject();
	}
}
