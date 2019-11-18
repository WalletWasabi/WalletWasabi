using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class GuidJsonConverter : JsonConverter<Guid>
	{
		public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => Guid.Parse(value));

		public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToString("N"));
	}
}
