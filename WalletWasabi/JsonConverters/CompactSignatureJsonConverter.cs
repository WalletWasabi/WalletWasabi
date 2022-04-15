using NBitcoin;
using NBitcoin.Secp256k1;
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

		if (bytes.Length != 65)
		{
			throw new ArgumentOutOfRangeException("Correct compact signatures are serialized as 65 bytes long arrays.");
		}

		int recoveryId = ComputeRecoveryId(bytes[0]);
		byte[] signature = bytes[1..];

		CompactSignature compactSignature = new(recoveryId, signature);

		if (!SecpRecoverableECDSASignature.TryCreateFromCompact(compactSignature.Signature, compactSignature.RecoveryId, out _))
		{
			throw new InvalidOperationException("Compact signature is not valid.");
		}

		return compactSignature;
	}

	/// <remarks>
	/// See commit https://github.com/MetacoSA/NBitcoin/commit/48341d508ef306dd7cfc1f50539acbd3b847ba23 and the original <c>NBitcoin/PubKey.cs:471</c> file.
	/// This method mimics the old code. Especially, no constant was modified.
	/// </remarks>
	private static int ComputeRecoveryId(byte b)
	{
		int header = b;

		if (header < 27 || header > 34)
		{
			throw new ArgumentOutOfRangeException($"Header byte out of range: {header}");
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

	/// <remarks>
	/// See commit https://github.com/MetacoSA/NBitcoin/commit/48341d508ef306dd7cfc1f50539acbd3b847ba23 and the original <c>NBitcoin/Key.cs:237</c> file.
	/// This method mimics the old code. Especially, no constant was modified.
	/// </remarks>
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
