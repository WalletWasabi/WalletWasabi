using Newtonsoft.Json;

namespace WalletWasabi.WabiSabi.Crypto.Serialization;

public static class JsonReaderExtensions
{
	/// <exception cref="JsonException">If the property is not found, or its value is <c>null</c>.</exception>
	public static T ReadProperty<T>(this JsonReader reader, JsonSerializer serializer, string name)
	{
		if (!reader.Read())
		{
			throw new JsonException($"Property '{name}' was expected.");
		}

		if (reader.TokenType == JsonToken.PropertyName)
		{
			if (reader.Value?.ToString() is { } propertyName)
			{
				if (!propertyName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
				{
					throw new JsonException($"Property '{name}' was expected.");
				}

				reader.Read();

				T t = serializer.Deserialize<T>(reader)
					?? throw new JsonException($"Unexpected null value for '{name}' property.");

				return t;
			}
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
