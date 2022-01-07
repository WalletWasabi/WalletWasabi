using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Numerics;
using WalletWasabi.Crypto;

namespace WalletWasabi.JsonConverters;

public class UnblindedSignatureJsonConverter : JsonConverter
{
	/// <inheritdoc />
	public override bool CanConvert(Type objectType)
	{
		return objectType == typeof(UnblindedSignature);
	}

	/// <inheritdoc />
	public override object ReadJson(JsonReader reader, Type? objectType, object? existingValue, JsonSerializer? serializer)
	{
		JArray arr = JArray.Load(reader);

		var carr = ToFixedLengthByteArray(StringToBigInteger(arr[0].Value<string>()));
		var sarr = ToFixedLengthByteArray(StringToBigInteger(arr[1].Value<string>()));

		var signatureBytes = carr.Concat(sarr).ToArray();
		var signature = ByteHelpers.ToHex(signatureBytes);

		var sig = UnblindedSignature.Parse(signature);
		return sig;
	}

	private BigInteger StringToBigInteger(string num)
	{
		if (string.IsNullOrWhiteSpace(num) || num.Length > 78)
		{
			throw new FormatException("UnblindedSignature components C or S are not valid.");
		}
		var bi = BigInteger.Parse(num);
		if (bi.IsZero || bi < BigInteger.Zero)
		{
			throw new FormatException("UnblindedSignature components C or S are zero or negative.");
		}
		return bi;
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, object value, JsonSerializer? serializer)
	{
		var signature = (UnblindedSignature)value;
		var c = new BigInteger(signature.C.ToBytes(), isUnsigned: true, isBigEndian: true);
		var s = new BigInteger(signature.S.ToBytes(), isUnsigned: true, isBigEndian: true);
		writer.WriteStartArray();
		writer.WriteValue(c.ToString());
		writer.WriteValue(s.ToString());
		writer.WriteEndArray();
	}

	private static byte[] ToFixedLengthByteArray(BigInteger bi)
	{
		var arr = bi.ToByteArray(true, true);

		if (arr.Length > 32)
		{
			throw new FormatException("UnblindedSignature components C or S are longer than 32 bytes.");
		}
		if (arr.Length < 32)
		{
			arr = new byte[32 - arr.Length].Concat(arr).ToArray();
		}
		return arr;
	}
}
