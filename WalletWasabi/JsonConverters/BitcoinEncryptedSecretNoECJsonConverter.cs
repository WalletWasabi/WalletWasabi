using NBitcoin;
using Newtonsoft.Json;
using System;

namespace WalletWasabi.JsonConverters
{
	public class BitcoinEncryptedSecretNoECJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(BitcoinEncryptedSecretNoEC);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var value = reader.Value as string;

			if (string.IsNullOrWhiteSpace(value))
			{
				return null;
			}

			return new BitcoinEncryptedSecretNoEC(value);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(((BitcoinEncryptedSecretNoEC)value).ToWif());
		}
	}
}
