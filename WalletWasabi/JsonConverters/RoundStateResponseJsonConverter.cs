using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WalletWasabi.CoinJoin.Common.Models;

namespace WalletWasabi.JsonConverters;

public class RoundStateResponseJsonConverter : JsonConverter
{
	public RoundStateResponseJsonConverter(ushort protocolVersion)
	{
		ProtocolVersion = protocolVersion;
	}

	public override bool CanWrite => false;

	public ushort ProtocolVersion { get; }

	public override bool CanConvert(Type objectType) => typeof(RoundStateResponseBase) == objectType;

	public override object? ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
	{
		if (reader.TokenType == JsonToken.Null)
		{
			return null;
		}

		var jobject = JObject.Load(reader);

		var type = ProtocolVersion switch
		{
			4 => typeof(RoundStateResponse4),
			_ => throw new InvalidOperationException($"Cannot deserialize message for unknown protocol version: {ProtocolVersion}")
		};

		if (type is null)
		{
			throw new JsonSerializationException("Could not determine object type.");
		}
		return jobject.ToObject(type, serializer);
	}

	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	{
		throw new NotImplementedException();
	}
}
