using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters
{
	public class BitcoinAddressJsonConverter : JsonConverter<BitcoinAddress>
	{
		public override BitcoinAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => NBitcoinHelpers.BetterParseBitcoinAddress(value));

		public override void Write(Utf8JsonWriter writer, BitcoinAddress value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToString());
	}
}
