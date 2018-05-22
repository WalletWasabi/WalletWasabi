using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using System;

namespace WalletWasabi.Crypto
{
	public class BlindingRsaPubKey : IEquatable<BlindingRsaPubKey>
	{
		public RsaKeyParameters KeyParameters { get; private set; }
		public BigInteger Modulus => KeyParameters.Modulus;
		public BigInteger Exponent => KeyParameters.Exponent;

		public BlindingRsaPubKey(RsaKeyParameters keyParameters)
		{
			KeyParameters = keyParameters ?? throw new ArgumentNullException(nameof(keyParameters));
		}

		public BlindingRsaPubKey(BigInteger modulus, BigInteger exponent)
		{
			if (modulus == null) throw new ArgumentNullException(nameof(modulus));
			if (exponent == null) throw new ArgumentNullException(nameof(exponent));

			KeyParameters = new RsaKeyParameters(false, modulus, exponent);
		}

		public (BigInteger BlindingFactor, byte[] BlindedData) Blind(byte[] data)
		{
			// generate blinding factor with pubkey
			var blindingFactorGenerator = new RsaBlindingFactorGenerator();
			blindingFactorGenerator.Init(KeyParameters);
			BigInteger blindingFactor = blindingFactorGenerator.GenerateBlindingFactor();

			// blind data
			var blindingParams = new RsaBlindingParameters(KeyParameters, blindingFactor);
			var blinder = new PssSigner(
				cipher: new RsaBlindingEngine(),
				digest: new Sha256Digest(),
				saltLen: 32);
			blinder.Init(forSigning: true, parameters: blindingParams);
			blinder.BlockUpdate(data, 0, data.Length);
			byte[] blindedData = blinder.GenerateSignature();

			return (blindingFactor, blindedData);
		}

		/// <returns>unblinded signature</returns>
		public byte[] UnblindSignature(byte[] blindedSignature, BigInteger blindingFactor)
		{
			var blindingEngine = new RsaBlindingEngine();
			var blindingParams = new RsaBlindingParameters(KeyParameters, blindingFactor);
			blindingEngine.Init(forEncryption: false, param: blindingParams);
			return blindingEngine.ProcessBlock(blindedSignature, 0, blindedSignature.Length);
		}

		public bool Verify(byte[] signature, byte[] data)
		{
			var verifier = new PssSigner(
				cipher: new RsaEngine(),
				digest: new Sha256Digest(),
				saltLen: 32);
			verifier.Init(forSigning: false, parameters: KeyParameters);
			verifier.BlockUpdate(data, 0, data.Length);
			return verifier.VerifySignature(signature);
		}

		public string ToJson()
		{
			dynamic json = new JObject();
			json.Modulus = Modulus.ToString();
			json.Exponent = Exponent.ToString();

			return json.ToString();
		}

		public static BlindingRsaPubKey CreateFromJson(string json)
		{
			var token = JToken.Parse(json);
			var mod = new BigInteger(token.Value<string>("Modulus"));
			var exp = new BigInteger(token.Value<string>("Exponent"));

			return new BlindingRsaPubKey(mod, exp);
		}

		#region Equality

		public override bool Equals(object obj) => obj is BlindingRsaPubKey && this == (BlindingRsaPubKey)obj;

		public bool Equals(BlindingRsaPubKey other) => this == other;

		public override int GetHashCode()
		{
			var hash = Modulus.GetHashCode();
			hash = hash ^ Exponent.GetHashCode();

			return hash;
		}

		public static bool operator ==(BlindingRsaPubKey x, BlindingRsaPubKey y)
		{
			if (x == null && y == null) return true;
			if (x == null ^ y == null) return false;
			
			return
				x.Modulus.Equals(y.Modulus)
				&& x.Exponent.Equals(y.Exponent);
		}

		public static bool operator !=(BlindingRsaPubKey x, BlindingRsaPubKey y) => !(x == y);

		#endregion Equality
	}
}
