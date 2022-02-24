using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace WalletWasabi.JsonConverters;

public class CompactSignatureJsonConverter : JsonConverter<CompactSignature>
{
	/// <inheritdoc />
	public override CompactSignature? ReadJson(JsonReader reader, Type objectType, CompactSignature? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var value = reader.Value as string;

		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		var ba = Convert.FromBase64String(value);
		var recoveryId = BitConverter.ToInt32(ba.AsSpan()[0..sizeof(int)]);
		var signature = ba[sizeof(int)..];

		return new CompactSignature(recoveryId, signature);
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, CompactSignature? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			throw new ArgumentNullException(nameof(value));
		}
		else
		{
			List<byte> ba = new(BitConverter.GetBytes(value.RecoveryId));
			ba.AddRange(value.Signature);

			writer.WriteValue(Convert.ToBase64String(ba.ToArray()));
		}
	}
}
