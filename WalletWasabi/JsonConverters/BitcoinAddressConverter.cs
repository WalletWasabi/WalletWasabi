using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters
{
	public class BitcoinAddressConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(BitcoinAddress);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var serialized = (string)reader.Value;

			return NBitcoinHelpers.ParseBitcoinAddress(serialized);
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var address = (BitcoinAddress)value;

			writer.WriteValue(address.ToString());
		}
	}
}
