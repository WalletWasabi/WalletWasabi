using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class FeeRatePerKbJsonConverter : JsonConverter<FeeRate>
	{
		public override FeeRate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> new FeeRate(new Money(reader.GetInt64()));

		public override void Write(Utf8JsonWriter writer, FeeRate value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.FeePerK.Satoshi.ToString());
	}
}
