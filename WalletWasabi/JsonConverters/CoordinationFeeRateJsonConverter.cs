using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.JsonConverters
{
	public class CoordinationFeeRateJsonConverter : JsonConverter<CoordinationFeeRate>
	{
		/// <inheritdoc />
		public override CoordinationFeeRate ReadJson(JsonReader reader, Type objectType, CoordinationFeeRate existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			JObject item = JObject.Load(reader);

			var rate = item.GetValue("Rate", StringComparison.OrdinalIgnoreCase)?.Value<decimal>() ?? throw new JsonSerializationException("Rate is null.");
			var plebsDontPayThreshold = item.GetValue("PlebsDontPayThreshold", StringComparison.OrdinalIgnoreCase)?.Value<long>() ?? throw new JsonSerializationException("PlebsDontPayThreshold is null.");
			return new CoordinationFeeRate(rate, Money.Satoshis(plebsDontPayThreshold));
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, CoordinationFeeRate value, JsonSerializer serializer)
		{
			writer.WriteStartObject();
			writer.WritePropertyName("Rate");
			writer.WriteValue(value.Rate);
			writer.WritePropertyName("PlebsDontPayThreshold");
			writer.WriteValue(value.PlebsDontPayThreshold);
			writer.WriteEndObject();
		}
	}
}
