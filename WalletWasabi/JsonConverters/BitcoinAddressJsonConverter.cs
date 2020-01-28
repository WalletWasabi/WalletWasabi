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
			var bitcoinAddressString = reader.Value as string;
			if (string.IsNullOrWhiteSpace(bitcoinAddressString))
			{
				return default;
			}
			else
			{
				return NBitcoinHelpers.BetterParseBitcoinAddress(bitcoinAddressString);
			}
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var bitcoinAddress = value as BitcoinAddress;

			writer.WriteValue(bitcoinAddress.ToString());
		}
	}
}
