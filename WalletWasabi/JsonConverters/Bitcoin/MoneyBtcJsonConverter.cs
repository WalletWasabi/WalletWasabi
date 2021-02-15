using NBitcoin;
using Newtonsoft.Json;
using System;

namespace WalletWasabi.JsonConverters.Bitcoin
{
	public class MoneyBtcJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(Money);
		}

		/// <inheritdoc />
		public override object? ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var stringValue = reader.Value as string;
			return Parse(stringValue);
		}

		public static Money? Parse(string? stringValue)
		{
			if (string.IsNullOrWhiteSpace(stringValue))
			{
				return null;
			}
			else
			{
				return Money.Parse(stringValue);
			}
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var money = (Money)value;

			writer.WriteValue(money.ToString(fplus: false, trimExcessZero: true));
		}
	}
}
