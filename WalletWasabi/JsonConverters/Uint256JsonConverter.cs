using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class Uint256JsonConverter : JsonConverter<uint256>
	{
		public override uint256 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => new uint256(value));

		public override void Write(Utf8JsonWriter writer, uint256 value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToString());
	}
}
