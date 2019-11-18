using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class KeyJsonConverter : JsonConverter<Key>
	{
		public override Key Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => Key.Parse(value));

		public override void Write(Utf8JsonWriter writer, Key value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.GetWif(Network.Main).ToString());
	}
}
