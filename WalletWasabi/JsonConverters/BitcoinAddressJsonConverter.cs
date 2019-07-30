using NBitcoin;
using Newtonsoft.Json;
using System;
using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters
{
	public class BitcoinAddressJsonConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(BitcoinAddress);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var value = Guard.Correct((string)reader.Value);

			return string.IsNullOrWhiteSpace(value) ? default : Network.Parse<BitcoinAddress>(value);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var address = (BitcoinAddress)value;

			writer.WriteValue(address.ToString());
		}
	}
}
