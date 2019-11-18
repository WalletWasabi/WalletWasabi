using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class HDFingerprintJsonConverter : JsonConverter<HDFingerprint>
	{
		public override HDFingerprint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => new HDFingerprint(ByteHelpers.FromHex(value)));

		public override void Write(Utf8JsonWriter writer, HDFingerprint value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToString());
	}
}
