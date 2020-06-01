using System;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.Secp256k1;

namespace WalletWasabi.Crypto
{
	public class SchnorrBlinding
	{
		public class Requester
		{
#pragma warning disable IDE1006 // Naming Styles

			private Scalar _v = Scalar.Zero;
			private Scalar _c = Scalar.Zero;
			private Scalar _w = Scalar.Zero;

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
				_v = new Scalar(tmp, out int overflow);
				if (overflow != 0 || _v.IsZero)
				{
					goto retry;
				}

				RandomUtils.GetBytes(tmp);
				_w = new Scalar(tmp, out overflow);
				if (overflow != 0 || _v.IsZero)
				{
					goto retry;
				}

				var A1 = ctx.MultGen(_v);
				var A2 = _w * P;
				var A = R.AddVariable(A1, out _).AddVariable(A2, out _).ToGroupElement();
				t = A.x.Normalize();
				if (t.IsZero)
				{
					goto retry;
				}

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
				{
					goto retry;
				}

				var cp = _c.Add(_w.Negate(), out overflow); // this is sent to the signer (blinded message)
				if (cp.IsZero || overflow != 0)
				{
					goto retry;
				}

				cp.WriteToSpan(tmp);
				return new uint256(tmp);
			}

			public UnblindedSignature UnblindSignature(uint256 blindSignature)
			{
				Span<byte> tmp = stackalloc byte[32];
				blindSignature.ToBytes(tmp);
				var sp = new Scalar(tmp, out int overflow);
				if (sp.IsZero || overflow != 0)
				{
					throw new ArgumentException("Invalid blindSignature", nameof(blindSignature));
				}

				var s = sp + _v;
				if (s.IsZero || s.IsOverflow)
				{
					throw new ArgumentException("Invalid blindSignature", nameof(blindSignature));
				}

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
			// The signer key used for signing
			public Key Key { get; }

			public Signer(Key key)
			{
				Key = key;
			}

			public uint256 Sign(uint256 blindedMessage, Key rKey)
			{
				Span<byte> tmp = stackalloc byte[32];
				blindedMessage.ToBytes(tmp);
				var cp = new Scalar(tmp, out int overflow);
				if (cp.IsZero || overflow != 0)
					throw new System.ArgumentException("Invalid blinded message.", nameof(blindedMessage));
				if (!Context.Instance.TryCreateECPrivKey(rKey.ToBytes(), out var r))
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
				return VerifySignature(dataHash, signature, Key.PubKey);
			}

			public bool VerifyUnblindedSignature(UnblindedSignature signature, byte[] data)
			{
				var hash = new uint256(Hashes.SHA256(data));
				return VerifySignature(hash, signature, Key.PubKey);
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

#pragma warning restore IDE1006 // Naming Styles
	}
}
