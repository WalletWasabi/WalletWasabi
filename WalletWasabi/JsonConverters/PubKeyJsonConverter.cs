using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class PubKeyJsonConverter : JsonConverter<PubKey>
	{
		public override PubKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => new PubKey(value));

		public override void Write(Utf8JsonWriter writer, PubKey value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToHex());
	}
}
