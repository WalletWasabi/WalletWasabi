using NBitcoin;
using Newtonsoft.Json;
using System;

namespace WalletWasabi.JsonConverters
{
	public class FeeRatePerKbJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(FeeRate);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var value = long.Parse(reader.Value.ToString());
			return new FeeRate(new Money(value));
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(((FeeRate)value).FeePerK.Satoshi.ToString());
		}
	}
}
