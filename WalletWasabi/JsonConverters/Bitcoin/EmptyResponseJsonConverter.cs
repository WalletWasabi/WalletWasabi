using Newtonsoft.Json;
using System;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.JsonConverters.Bitcoin
{
	public class EmptyResponseJsonConverter : JsonConverter<EmptyResponse>
	{
		/// <inheritdoc />
		public override EmptyResponse? ReadJson(JsonReader reader, Type objectType, EmptyResponse? existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.StartObject)
			{
				bool closeBrace = reader.Read();
				if (closeBrace && reader.TokenType == JsonToken.EndObject)
				{
					return EmptyResponse.Instance;
				}
			}
			else if (reader.TokenType == JsonToken.Null)
			{
				return null;
			}

			throw new JsonException("Invalid JSON received.");
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, EmptyResponse? value, JsonSerializer serializer)
		{
			if (value is null)
			{
				writer.WriteNull();
			}
			else
			{
				writer.WriteStartObject();
				writer.WriteEndObject();
			}
		}
	}
}
