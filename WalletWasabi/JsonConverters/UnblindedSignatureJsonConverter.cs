using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class UnblindedSignatureJsonConverter : JsonConverter<UnblindedSignature>
	{
		public override UnblindedSignature Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			return new UnblindedSignature(new BigInteger(reader.GetString()), new BigInteger(reader.GetString()));
		}

		public override void Write(Utf8JsonWriter writer, UnblindedSignature value, JsonSerializerOptions options)
		{
			writer.WriteStartArray();
			writer.WriteStringValue(value.C.ToString());
			writer.WriteStringValue(value.S.ToString());
			writer.WriteEndArray();
		}
	}
}
