using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class BlockCypherDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
	{
		public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal));

		public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
	}
}
