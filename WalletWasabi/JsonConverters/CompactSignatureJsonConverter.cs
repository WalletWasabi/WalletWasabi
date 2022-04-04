using NBitcoin;
using Newtonsoft.Json;

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

		byte[] bytes = Convert.FromBase64String(value);
		int recoveryId = ComputeRecoveryId(bytes[0]);
		byte[] signature = bytes[1..];

		return new CompactSignature(recoveryId, signature);
	}

	private static int ComputeRecoveryId(byte b)
	{
		int header = b;

		if (header < 27 || header > 34)
		{
			throw new ArgumentException("Header byte out of range: " + header);
		}

		// This means: Compressed = true.
		if (header >= 31)
		{
			header -= 4;
		}
		else
		{
			throw new ArgumentException("Only compressed are supported.");
		}

		int recoveryId = header - 27;
		return recoveryId;
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
			byte[] sigBytes = ToBytes(value);
			writer.WriteValue(Convert.ToBase64String(sigBytes));
		}
	}

	internal static byte[] ToBytes(CompactSignature compactSignature)
	{
		bool isCompressed = true;

		byte[] result = new byte[65];
		result[0] = (byte)(27 + compactSignature.RecoveryId + (isCompressed ? 4 : 0));

		for (int i = 0; i < 64; i++)
		{
			result[i + 1] = compactSignature.Signature[i];
		}

		return result;
	}
}
