using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Models;

namespace WalletWasabi.JsonConverters
{
	public class HeightJsonConverter : JsonConverter<Height>
	{
		public override Height Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => new Height(int.Parse(value)));

		public override void Write(Utf8JsonWriter writer, Height value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToString());
	}
}
