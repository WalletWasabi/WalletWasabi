using NBitcoin;
using Newtonsoft.Json;
using System;

namespace WalletWasabi.JsonConverters.Bitcoin
{
	public class FeeRateJsonConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(FeeRate);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			return new FeeRate(Money.Satoshis((long)reader.Value));
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var feerate = (FeeRate)value;

			writer.WriteValue(feerate.FeePerK.Satoshi);
		}
	}
}
