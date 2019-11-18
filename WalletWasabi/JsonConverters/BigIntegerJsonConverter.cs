using NBitcoin.BouncyCastle.Math;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class BigIntegerJsonConverter : JsonConverter<BigInteger>
	{
		public override BigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => new BigInteger(value));

		public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToString());
	}
}
