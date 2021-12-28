using System.Diagnostics.CodeAnalysis;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;

namespace WalletWasabi.Crypto
{
	public class UnblindedSignature
	{
		internal UnblindedSignature(in Scalar c, in Scalar s)
		{
			C = c;
			S = s;
		}

		internal Scalar C { get; }
		internal Scalar S { get; }

		public static bool TryParse(ReadOnlySpan<byte> in64, [NotNullWhen(true)] out UnblindedSignature? unblindedSignature)
		{
			unblindedSignature = null;
			if (in64.Length != 64)
			{
				return false;
			}

			var c = new Scalar(in64.Slice(0, 32), out int overflow);
			if (c.IsZero || overflow != 0)
			{
				return false;
			}

			var s = new Scalar(in64.Slice(32, 32), out overflow);
			if (s.IsZero || overflow != 0)
			{
				return false;
			}

			unblindedSignature = new UnblindedSignature(c, s);
			return true;
		}

		public static UnblindedSignature Parse(ReadOnlySpan<byte> in64)
		{
			if (TryParse(in64, out var unblindedSignature))
			{
				return unblindedSignature;
			}

			throw new FormatException("Invalid unblinded signature");
		}

		public static UnblindedSignature Parse(string str)
		{
			if (TryParse(str, out var unblindedSignature))
			{
				return unblindedSignature;
			}

			throw new FormatException("Invalid unblinded signature");
		}

		public static bool TryParse(string str, [NotNullWhen(true)] out UnblindedSignature? unblindedSignature)
		{
			unblindedSignature = null;
			if (str is null)
			{
				throw new ArgumentNullException(nameof(str));
			}

			if (!HexEncoder.IsWellFormed(str))
			{
				return false;
			}

			return TryParse(Encoders.Hex.DecodeData(str), out unblindedSignature);
		}

		public byte[] ToBytes()
		{
			var result = new byte[64];
			ToBytes(result.AsSpan());
			return result;
		}

		public void ToBytes(Span<byte> out64)
		{
			if (out64.Length != 64)
			{
				throw new ArgumentException(paramName: nameof(out64), message: "out64 must be 64 bytes");
			}

			C.WriteToSpan(out64.Slice(0, 32));
			S.WriteToSpan(out64.Slice(32, 32));
		}

		public override string ToString()
		{
			Span<byte> tmp = stackalloc byte[64];
			ToBytes(tmp);
			return Encoders.Hex.EncodeData(tmp);
		}
	}
}
