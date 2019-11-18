using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class DateTimeOffsetUnixSecondsConverter : JsonConverter<DateTimeOffset>
	{
		public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => DateTimeOffset.FromUnixTimeSeconds(long.Parse(value)));

		public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToUnixTimeSeconds().ToString());
	}
}
