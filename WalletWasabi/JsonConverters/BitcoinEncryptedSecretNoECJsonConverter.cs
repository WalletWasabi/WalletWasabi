using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters
{
	public class BitcoinEncryptedSecretNoECJsonConverter : JsonConverter<BitcoinEncryptedSecretNoEC>
	{
		/// <inheritdoc />
		public override BitcoinEncryptedSecretNoEC? ReadJson(JsonReader reader, Type objectType, BitcoinEncryptedSecretNoEC? existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			var value = reader.Value as string;

			if (string.IsNullOrWhiteSpace(value))
			{
				return null;
			}

			return new BitcoinEncryptedSecretNoEC(value, Network.Main); // The `network` is required but the encoding doesn't depend on it.
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, BitcoinEncryptedSecretNoEC? value, JsonSerializer serializer)
		{
			if (value is null)
			{
				writer.WriteNull();
			}
			else
			{
				writer.WriteValue(value.ToWif());
			}
		}
	}
}
