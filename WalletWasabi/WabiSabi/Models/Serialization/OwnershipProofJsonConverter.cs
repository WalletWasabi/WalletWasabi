using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Models.Serialization;

public class OwnershipProofJsonConverter : JsonConverter<OwnershipProof>
{
	/// <inheritdoc />
	public override OwnershipProof? ReadJson(JsonReader reader, Type objectType, OwnershipProof? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.Value is string serialized)
		{
			return OwnershipProof.FromBytes(ByteHelpers.FromHex(serialized));
		}
		throw new ArgumentException($"No valid serialized {nameof(OwnershipProof)} passed.");
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, OwnershipProof? value, JsonSerializer serializer)
	{
		var bytes = value.ToBytes();
		writer.WriteValue(ByteHelpers.ToHex(bytes));
	}
}
