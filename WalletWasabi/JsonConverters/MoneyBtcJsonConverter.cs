using NBitcoin;
using System;

namespace WalletWasabi.JsonConverters
{
	public class MoneyBtcJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(Money);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var serialized = (string)reader.Value;

			return Money.Parse(serialized);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var money = (Money)value;

			writer.WriteValue(money.ToString(fplus: false, trimExcessZero: true));
		}
	}
}
