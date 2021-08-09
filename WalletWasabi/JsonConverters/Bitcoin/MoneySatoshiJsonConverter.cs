using NBitcoin;
using Newtonsoft.Json;
using System;

namespace WalletWasabi.JsonConverters.Bitcoin
{
	public class MoneySatoshiJsonConverter : JsonConverter<Money>
	{
		/// <inheritdoc />
		public override Money ReadJson(JsonReader reader, Type objectType, Money existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			var serialized = (long)reader.Value;

			return new Money(serialized);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, Money value, JsonSerializer serializer)
		{
			writer.WriteValue(value.Satoshi);
		}
	}
}
