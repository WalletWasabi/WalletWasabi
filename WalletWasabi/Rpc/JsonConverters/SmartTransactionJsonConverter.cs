using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Rpc.JsonConverters;

public class SmartTransactionJsonConverter : JsonConverter<SmartTransaction>
{
	public override SmartTransaction? ReadJson(JsonReader reader, Type objectType, SmartTransaction? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		throw new NotImplementedException();
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, SmartTransaction? value, JsonSerializer serializer)
	{
		serializer.Serialize(writer, value?.Transaction);
	}
}
