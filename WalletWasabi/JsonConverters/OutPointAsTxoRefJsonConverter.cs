using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WalletWasabi.JsonConverters;

public class OutPointAsTxoRefJsonConverter : JsonConverter
{
	/// <inheritdoc />
	public override bool CanConvert(Type objectType)
	{
		return objectType == typeof(OutPoint);
	}

	/// <inheritdoc />
	public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
	{
		JObject item = JObject.Load(reader);

		var hash = item.GetValue("TransactionId", StringComparison.OrdinalIgnoreCase).Value<string>();
		var n = item.GetValue("Index", StringComparison.OrdinalIgnoreCase).Value<uint>();
		return new OutPoint(uint256.Parse(hash), n);
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	{
		var outpoint = value as OutPoint;
		writer.WriteStartObject();
		writer.WritePropertyName("TransactionId");
		writer.WriteValue(outpoint.Hash.ToString());
		writer.WritePropertyName("Index");
		writer.WriteValue(outpoint.N);
		writer.WriteEndObject();
	}
}
