using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class ScriptJsonConverter : JsonConverter<Script>
	{
		public override Script Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => new Script(value));

		public override void Write(Utf8JsonWriter writer, Script value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToString());
	}
}
