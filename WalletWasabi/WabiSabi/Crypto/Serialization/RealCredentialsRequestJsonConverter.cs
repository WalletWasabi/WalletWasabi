using Newtonsoft.Json;
using WabiSabi;
using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.WabiSabi.Crypto.Serialization;

public class RealCredentialsRequestJsonConverter : JsonConverter<RealCredentialsRequest>
{
	public override RealCredentialsRequest? ReadJson(JsonReader reader, Type objectType, RealCredentialsRequest? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		reader.Expect(JsonToken.StartObject);
		var delta = reader.ReadProperty<long>(serializer, "Delta");
		var presented = reader.ReadProperty<CredentialPresentation>(serializer, "Presented");
		var requested = reader.ReadProperty<IssuanceRequest>(serializer, "Requested");
		var proofs = reader.ReadProperty<Proof>(serializer, "Proofs");
		reader.Read();
		reader.Expect(JsonToken.EndObject);
		return ReflectionUtils.CreateInstance<RealCredentialsRequest>(new object[] { delta, presented, requested, proofs });
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, RealCredentialsRequest? realCredentialsRequest, JsonSerializer serializer)
	{
		if (realCredentialsRequest is null)
		{
			throw new ArgumentException($"No valid {nameof(RealCredentialsRequest)}.", nameof(realCredentialsRequest));
		}
		writer.WriteStartObject();
		writer.WriteProperty("Delta", realCredentialsRequest.Delta, serializer);
		writer.WriteProperty("Presented", realCredentialsRequest.Presented, serializer);
		writer.WriteProperty("Requested", realCredentialsRequest.Requested, serializer);
		writer.WriteProperty("Proofs", realCredentialsRequest.Proofs, serializer);
		writer.WriteEndObject();
	}
}
