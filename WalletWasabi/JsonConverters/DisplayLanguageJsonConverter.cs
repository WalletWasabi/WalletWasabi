using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Models;

namespace WalletWasabi.JsonConverters;

public class DisplayLanguageJsonConverter : JsonConverter<int>
{
	public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.Number && reader.TokenType != JsonTokenType.Null)
		{
			throw new JsonException("Expected a JSON string value.");
		}

		var value = reader.GetInt32();

		if (Enum.IsDefined(typeof(DisplayLanguage), value))
		{
			return value;
		}

		return (int)DisplayLanguage.English;
	}

	public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
	{
		writer.WriteNumberValue(value);
	}
}
