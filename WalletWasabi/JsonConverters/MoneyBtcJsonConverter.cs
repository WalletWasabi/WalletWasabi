using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class MoneyBtcJsonConverter : JsonConverter<Money>
	{
		public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => Money.Parse(value));

		public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToString(fplus: false, trimExcessZero: true));
	}
}
