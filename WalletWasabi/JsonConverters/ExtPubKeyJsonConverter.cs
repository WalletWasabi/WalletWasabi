using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters
{
	public class ExtPubKeyJsonConverter : JsonConverter<ExtPubKey>
	{
		public override ExtPubKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => NBitcoinHelpers.BetterParseExtPubKey(value));

		public override void Write(Utf8JsonWriter writer, ExtPubKey value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.GetWif(Network.Main).ToWif());
	}
}
