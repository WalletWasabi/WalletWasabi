using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.WabiSabi.Models.Serialization;

public class WitScriptJsonConverter : JsonConverter
{
	public override bool CanConvert(Type objectType)
	{
		return objectType == typeof(WitScript);
	}

	public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
	{
		var value = (string)reader.Value;
		return new WitScript(ByteHelpers.FromHex(value));
	}

	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	{
		var bytes = ((WitScript)value).ToBytes();
		writer.WriteValue(ByteHelpers.ToHex(bytes));
	}
}
