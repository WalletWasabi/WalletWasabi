using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WalletWasabi.Rpc.JsonConverters;

public class OutPointAsTxoRefJsonConverter : JsonConverter<OutPoint>
{
	/// <inheritdoc />
	public override OutPoint? ReadJson(JsonReader reader, Type objectType, OutPoint? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		JObject item = JObject.Load(reader);

		string? hash = item.GetValue("TransactionId", StringComparison.OrdinalIgnoreCase)?.Value<string>();
		uint? n = item.GetValue("Index", StringComparison.OrdinalIgnoreCase)?.Value<uint>();

		if (hash is null)
		{
			throw new ArgumentNullException(nameof(hash));
		}

		if (n is null)
		{
			throw new ArgumentNullException(nameof(n));
		}

		return new OutPoint(uint256.Parse(hash), n.Value);
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
