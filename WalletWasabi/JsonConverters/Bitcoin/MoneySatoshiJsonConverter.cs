using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters.Bitcoin
{
	public class MoneySatoshiJsonConverter : JsonConverter<Money>
	{
		/// <inheritdoc />
		public override Money? ReadJson(JsonReader reader, Type objectType, Money? existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			var serialized = (long?)reader.Value;

			return serialized is null ? null : new Money(serialized.Value);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, Money? value, JsonSerializer serializer)
		{
			if (value is null)
			{
				writer.WriteNull();
			}
			else
			{
				writer.WriteValue(value.Satoshi);
			}
		}
	}
}
