using System;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;

namespace WalletWasabi.Crypto
{
#nullable enable
	public class UnblindedSignature
	{
		internal Scalar C { get; }
		internal Scalar S { get; }

		internal UnblindedSignature(in Scalar c, in Scalar s)
		{
			C = c;
			S = s;
		}


		public static bool TryParse(ReadOnlySpan<byte> in64, out UnblindedSignature? unblindedSignature)
		{
			int overflow;
			unblindedSignature = null;
			if (in64.Length != 64)
				return false;
			var c = new Scalar(in64.Slice(0, 32), out overflow);
			if (c.IsZero || overflow != 0)
				return false;
			var s = new Scalar(in64.Slice(32, 32), out overflow);
			if (s.IsZero || overflow != 0)
				return false;
			unblindedSignature = new UnblindedSignature(c, s);
			return true;
		}

		public static UnblindedSignature Parse(ReadOnlySpan<byte> in64)
		{
			if (TryParse(in64, out var unblindedSignature) && unblindedSignature is UnblindedSignature)
				return unblindedSignature;
			throw new FormatException("Invalid unblinded signature");
		}
		public static UnblindedSignature Parse(string str)
		{
			if (TryParse(str, out var unblindedSignature) && unblindedSignature is UnblindedSignature)
				return unblindedSignature;
			throw new FormatException("Invalid unblinded signature");
		}
		public static bool TryParse(string str, out UnblindedSignature? unblindedSignature)
		{
			unblindedSignature = null;
			if (str == null)
				throw new ArgumentNullException(nameof(str));
			if (!HexEncoder.IsWellFormed(str))
				return false;
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
				throw new ArgumentException(paramName: nameof(out64), message: "out64 must be 64 bytes");
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
#nullable restore
	public class SchnorrBlinding
	{
		public class Requester
		{
			Scalar _v = Scalar.Zero;
			Scalar _c = Scalar.Zero;
			Scalar _w = Scalar.Zero;
			public Requester()
			{
			}

			public uint256 BlindScript(PubKey signerPubKey, PubKey rPubKey, Script script)
			{
				var msg = new uint256(Hashes.SHA256(script.ToBytes()));
				return BlindMessage(msg, rPubKey, signerPubKey);
			}

			public uint256 BlindMessage(uint256 message, PubKey rpubkey, PubKey signerPubKey)
			{
				var ctx = new ECMultGenContext();
				int overflow;
				Span<byte> tmp = stackalloc byte[32];
				
				if (!Context.Instance.TryCreatePubKey(signerPubKey.ToBytes(), out var signerECPubkey))
				{
					throw new FormatException("Invalid signer pubkey.");
				}
				if (!Context.Instance.TryCreatePubKey(rpubkey.ToBytes(), out var rECPubKey))
				{
					throw new FormatException("Invalid r pubkey.");
				}
				var P = signerECPubkey.Q;
				var R = rECPubKey.Q.ToGroupElementJacobian();
				var t = FE.Zero;
			retry:

				RandomUtils.GetBytes(tmp);
				_v = new Scalar(tmp, out overflow);
				if (overflow != 0 || _v.IsZero)
					goto retry;
				RandomUtils.GetBytes(tmp);
				_w = new Scalar(tmp, out overflow);
				if (overflow != 0 || _v.IsZero)
					goto retry;
				var A1 = ctx.MultGen(_v);
				var A2 = _w * P;
				var A = R.AddVariable(A1, out _).AddVariable(A2, out _).ToGroupElement();
				t = A.x.Normalize();
				if (t.IsZero)
					goto retry;
				using (var sha = new SHA256())
				{
					message.ToBytes(tmp, false);
					sha.Write(tmp);
					t.WriteToSpan(tmp);
					sha.Write(tmp);
					sha.GetHash(tmp);
				}
				_c = new Scalar(tmp, out overflow);
				if (overflow != 0 || _c.IsZero)
					goto retry;
				var cp = _c.Add(_w.Negate(), out overflow); // this is sent to the signer (blinded message)
				if (cp.IsZero || overflow != 0)
					goto retry;
				cp.WriteToSpan(tmp);
				return new uint256(tmp);
			}

			public UnblindedSignature UnblindSignature(uint256 blindSignature)
			{
				int overflow;
				Span<byte> tmp = stackalloc byte[32];
				blindSignature.ToBytes(tmp);
				var sp = new Scalar(tmp, out overflow);
				if (sp.IsZero || overflow != 0)
					throw new ArgumentException("Invalid blindSignature", nameof(blindSignature));
				var s = sp + _v;
				if (s.IsZero || s.IsOverflow)
					throw new ArgumentException("Invalid blindSignature", nameof(blindSignature));
				return new UnblindedSignature(_c, s);
			}

			public uint256 BlindMessage(byte[] message, PubKey rpubKey, PubKey signerPubKey)
			{
				var msg = new uint256(Hashes.SHA256(message));
				return BlindMessage(msg, rpubKey, signerPubKey);
			}
		}

		public class Signer
		{
			// The random generated r value. It is used to derivate an R point where
			// R = r*G that has to be sent to the requester in order to allow him to
			// blind the message to be signed.
			public Key R { get; }

			// The signer key used for signing
			public Key Key { get; }

			public Signer(Key key)
				: this(key, new Key())
			{ }

			public Signer(Key key, Key r)
			{
				R = r;
				Key = key;
			}

			public uint256 Sign(uint256 blindedMessage)
			{
				Span<byte> tmp = stackalloc byte[32];
				blindedMessage.ToBytes(tmp);
				var cp = new Scalar(tmp, out int overflow);
				if (cp.IsZero || overflow != 0)
					throw new System.ArgumentException("Invalid blinded message.", nameof(blindedMessage));
				if (!Context.Instance.TryCreateECPrivKey(R.ToBytes(), out var r))
				{
					throw new FormatException("Invalid key.");
				}
				if (!Context.Instance.TryCreateECPrivKey(Key.ToBytes(), out var d))
				{
					throw new FormatException("Invalid key.");
				}

				var sp = r.sec + (cp * d.sec).Negate();
				sp.WriteToSpan(tmp);
				return new uint256(tmp);
			}

			public bool VerifyUnblindedSignature(UnblindedSignature signature, uint256 dataHash)
			{
				return SchnorrBlinding.VerifySignature(dataHash, signature, Key.PubKey);
			}

			public bool VerifyUnblindedSignature(UnblindedSignature signature, byte[] data)
			{
				var hash = new uint256(Hashes.SHA256(data));
				return SchnorrBlinding.VerifySignature(hash, signature, Key.PubKey);
			}
		}

		public static bool VerifySignature(uint256 message, UnblindedSignature signature, PubKey signerPubKey)
		{
			if (!Context.Instance.TryCreatePubKey(signerPubKey.ToBytes(), out var signerECPubkey))
			{
				throw new FormatException("Invalid signer pubkey.");
			}

			var P = signerECPubkey.Q;

			var sG = (signature.S * EC.G).ToGroupElement();
			var cP = P * signature.C;
			var R = cP + sG;
			var t = R.ToGroupElement().x.Normalize();
			using var sha = new SHA256();
			Span<byte> tmp = stackalloc byte[32];
			message.ToBytes(tmp, false);
			sha.Write(tmp);
			t.WriteToSpan(tmp);
			sha.Write(tmp);
			sha.GetHash(tmp);
			return new Scalar(tmp) == signature.C;
		}
	}
}
