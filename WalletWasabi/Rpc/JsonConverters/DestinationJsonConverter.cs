using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Wallets.SilentPayment;

namespace WalletWasabi.Rpc.JsonConverters;

public class DestinationJsonConverter(Network Network): JsonConverter<Destination>
{
	public override void WriteJson(JsonWriter writer, Destination? value, JsonSerializer serializer)
	{
		var wip = value switch
		{
			Destination.Loudly l => l.ScriptPubKey.GetDestinationAddress(Network)?.ToString(),
			Destination.Silent s => s.Address.ToWip(Network),
			_ => throw new ArgumentException($"Unknown destination type: {value?.GetType().Name}")
		};
		writer.WriteValue(wip);
	}

	public override Destination? ReadJson(JsonReader reader, Type objectType, Destination? existingValue, bool hasExistingValue,
		JsonSerializer serializer)
	{
		var str = reader.Value as string;
		try
		{
			var address = BitcoinAddress.Create(str, Network);
			return new Destination.Loudly(address.ScriptPubKey);
		}
		catch (Exception)
		{
			var address = SilentPaymentAddress.Parse(str, Network);
			return new Destination.Silent(address);
		}
	}
}
