using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class BitcoinEncryptedSecretNoECJsonConverter : JsonConverter<BitcoinEncryptedSecretNoEC>
	{
		public override BitcoinEncryptedSecretNoEC Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => new BitcoinEncryptedSecretNoEC(value));

		public override void Write(Utf8JsonWriter writer, BitcoinEncryptedSecretNoEC value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToWif());
	}
}
