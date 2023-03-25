using Newtonsoft.Json;

namespace WalletWasabi.WabiSabi.Crypto.Serialization;

public static class JsonReaderExtensions
{
	public static T? ReadProperty<T>(this JsonReader reader, JsonSerializer serializer, string name)
	{
		if (!reader.Read())
		{
			throw new JsonException($"Property '{name}' was expected.");
		}

		if (reader.TokenType == JsonToken.PropertyName)
		{
			var propertyName = reader.Value.ToString();
			if (propertyName != name)
			{
				throw new JsonException($"Property '{name}' was expected.");
			}

			reader.Read();
			return serializer.Deserialize<T>(reader);
		}
		throw new JsonException($"Property '{name}' was expected.");
	}

	public static void Expect(this JsonReader reader, JsonToken expectedToken)
	{
		if (reader.TokenType != expectedToken)
		{
			throw new JsonException();
		}
	}
}
