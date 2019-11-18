using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class MoneySatoshiJsonConverter : JsonConverter<Money>
	{
		public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> new Money(reader.GetInt64());

		public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
			=> writer.WriteNumberValue(value.Satoshi);
	}
}
