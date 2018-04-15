using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Converters
{
	public class SatoshiConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(Money);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var serialized = (long)reader.Value;

			return new Money(serialized);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var money = (Money)value;

			writer.WriteValue(money.Satoshi);
		}
	}
}