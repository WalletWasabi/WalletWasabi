using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters;

public class TransactionJsonConverter : JsonConverter<Transaction>
{
	/// <inheritdoc />
	public override Transaction? ReadJson(JsonReader reader, Type objectType, Transaction? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var txHex = reader.Value?.ToString();
		var tx = Transaction.Parse(txHex, Network.Main);
		return tx;
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, Transaction? value, JsonSerializer serializer)
	{
		var txHex = value?.ToHex() ?? throw new ArgumentNullException(nameof(value));
		writer.WriteValue(txHex);
	}
}
