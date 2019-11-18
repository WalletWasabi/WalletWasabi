using NBitcoin;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WalletWasabi.JsonConverters
{
	public class TransactionJsonConverter : JsonConverter<Transaction>
	{
		public override Transaction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> reader.CreateObject(value => Transaction.Parse(value, Network.Main));

		public override void Write(Utf8JsonWriter writer, Transaction value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToHex());
	}
}
