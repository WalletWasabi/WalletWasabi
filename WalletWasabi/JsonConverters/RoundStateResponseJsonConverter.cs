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

		JObject jobject = JObject.Load(reader);

		if (ProtocolVersion != 4)
		{
			throw new InvalidOperationException($"Cannot deserialize message for unknown protocol version: {ProtocolVersion}");
		}

		return jobject.ToObject<RoundStateResponse4>();
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, RoundStateResponseBase? value, JsonSerializer serializer)
	{
		throw new NotImplementedException();
	}
}
