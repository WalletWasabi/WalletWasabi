using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class KeyPathJsonConverter : JsonConverter<KeyPath>
	{
		public override KeyPath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => KeyPath.Parse(value));

		public override void Write(Utf8JsonWriter writer, KeyPath value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToString());
	}
}
