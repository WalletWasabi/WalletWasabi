using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class ByteArrayJsonConverter : JsonConverter<byte[]>
	{
		public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => Convert.FromBase64String(value));

		public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
			=> writer.WriteStringValue(Convert.ToBase64String(value));
	}
}
