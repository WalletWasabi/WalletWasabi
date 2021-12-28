using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.JsonConverters
{
	public class CoordinationFeeRateJsonConverter : JsonConverter<CoordinationFeeRate>
	{
		/// <inheritdoc />
		public override CoordinationFeeRate ReadJson(JsonReader reader, Type objectType, CoordinationFeeRate existingValue, bool hasExistingValue, JsonSerializer serializer) =>
			reader.Value switch
			{
				double rate => new CoordinationFeeRate((decimal)rate),
				_ => throw new JsonSerializationException("CoordinationFeeRate is null.")
			};

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, CoordinationFeeRate value, JsonSerializer serializer) =>
			writer.WriteValue(value.Rate);
	}
}
