using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WalletWasabi.CoinJoin.Common.Models;

namespace WalletWasabi.JsonConverters;

public class RoundStateResponseJsonConverter : JsonConverter<RoundStateResponseBase>
{
	public RoundStateResponseJsonConverter(ushort protocolVersion)
	{
		ProtocolVersion = protocolVersion;
	}

	public override bool CanWrite => false;

	public ushort ProtocolVersion { get; }

	/// <inheritdoc />
	public override RoundStateResponseBase? ReadJson(JsonReader reader, Type objectType, RoundStateResponseBase? existingValue, bool hasExistingValue, JsonSerializer serializer)
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
		return (RoundStateResponseBase?)jobject.ToObject(type, serializer);
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, RoundStateResponseBase? value, JsonSerializer serializer)
	{
		throw new NotImplementedException();
	}
}
