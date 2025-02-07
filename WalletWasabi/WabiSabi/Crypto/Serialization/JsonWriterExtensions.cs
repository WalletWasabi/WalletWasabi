using Newtonsoft.Json;

namespace WalletWasabi.WabiSabi.Crypto.Serialization;

public static class JsonWriterExtensions
{
	public static void WriteProperty<T>(this JsonWriter writer, string name, T value, JsonSerializer serializer)
	{
		writer.WritePropertyName(name);
		serializer.Serialize(writer, value);
	}
}
