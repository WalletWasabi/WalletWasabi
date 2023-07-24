using Newtonsoft.Json;
using WabiSabi;
using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.WabiSabi.Crypto.Serialization;

public class ZeroCredentialsRequestJsonConverter : JsonConverter<ZeroCredentialsRequest>
{
	public override ZeroCredentialsRequest? ReadJson(JsonReader reader, Type objectType, ZeroCredentialsRequest? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		reader.Expect(JsonToken.StartObject);
		var delta = reader.ReadProperty<long>(serializer, "Delta");
		var presented = reader.ReadProperty<CredentialPresentation>(serializer, "Presented");
		var requested = reader.ReadProperty<IssuanceRequest>(serializer, "Requested");
		var proofs = reader.ReadProperty<Proof>(serializer, "Proofs");
		reader.Read();
		reader.Expect(JsonToken.EndObject);
		return ReflectionUtils.CreateInstance<ZeroCredentialsRequest>(new object[] { delta, presented, requested, proofs });
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, ZeroCredentialsRequest? zeroCredentialsRequest, JsonSerializer serializer)
	{
		if (zeroCredentialsRequest is null)
		{
			throw new ArgumentException($"No valid {nameof(ZeroCredentialsRequest)}.", nameof(zeroCredentialsRequest));
		}
		writer.WriteStartObject();
		writer.WriteProperty("Delta", zeroCredentialsRequest.Delta, serializer);
		writer.WriteProperty("Presented", zeroCredentialsRequest.Presented, serializer);
		writer.WriteProperty("Requested", zeroCredentialsRequest.Requested, serializer);
		writer.WriteProperty("Proofs", zeroCredentialsRequest.Proofs, serializer);
		writer.WriteEndObject();
	}
}
