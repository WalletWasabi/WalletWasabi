using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using System;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto
{
	public class BlindingRsaKey : IEquatable<BlindingRsaKey>
	{
		public AsymmetricCipherKeyPair KeyPair { get; private set; }

		public BlindingRsaPubKey PubKey => new BlindingRsaPubKey((RsaKeyParameters)KeyPair.Public);
		public BigInteger PrivateModulus => ((RsaKeyParameters)KeyPair.Private).Modulus;
		public BigInteger PrivateExponent => ((RsaKeyParameters)KeyPair.Private).Exponent;
		public BigInteger PublicModulus => ((RsaKeyParameters)KeyPair.Public).Modulus;
		public BigInteger PublicExponent => ((RsaKeyParameters)KeyPair.Public).Exponent;

		public BlindingRsaKey()
		{
			// Generate a 2048-bit RSA key pair.
			var generator = new RsaKeyPairGenerator();
			var RSA_F4 = Constants.RsaPubKeyExponent;
			generator.Init(new RsaKeyGenerationParameters(
						publicExponent: RSA_F4,
						random: new SecureRandom(),
						strength: 2048,
						certainty: 100)); // See A.15.2 IEEE P1363 v2 D1 for certainty parameter
			KeyPair = generator.GenerateKeyPair();
		}

		public BlindingRsaKey(AsymmetricCipherKeyPair keyPair)
		{
			if (keyPair is null)
				throw new ArgumentNullException(nameof(keyPair));
			if (keyPair.Private is null)
				throw new ArgumentNullException(nameof(keyPair.Private));
			if (keyPair.Public is null)
				throw new ArgumentNullException(nameof(keyPair.Public));
			KeyPair = keyPair;
		}

		public BlindingRsaKey(BigInteger privModulus, BigInteger privExponent, BigInteger pubModulus, BigInteger pubExponent)
		{
			if (privModulus is null) throw new ArgumentNullException(nameof(privModulus));
			if (privExponent is null) throw new ArgumentNullException(nameof(privExponent));
			if (pubModulus is null) throw new ArgumentNullException(nameof(pubModulus));
			if (pubExponent is null) throw new ArgumentNullException(nameof(pubExponent));

			var priv = new RsaKeyParameters(true, privModulus, privExponent);
			var pub = new RsaKeyParameters(false, pubModulus, pubExponent);

			KeyPair = new AsymmetricCipherKeyPair(pub, priv);
		}

		/// <returns>signature</returns>
		public byte[] SignBlindedData(byte[] blindedData)
		{
			var signer = new RsaEngine();
			signer.Init(forEncryption: false, parameters: KeyPair.Private);
			return signer.ProcessBlock(blindedData, 0, blindedData.Length);
		}

		public string ToJson()
		{
			dynamic json = new JObject();
			json.PrivateModulus = PrivateModulus.ToString();
			json.PrivateExponent = PrivateExponent.ToString();
			json.PublicModulus = PublicModulus.ToString();
			json.PublicExponent = PublicExponent.ToString();

			return json.ToString();
		}

		public static BlindingRsaKey CreateFromJson(string json)
		{
			var token = JToken.Parse(json);
			var privMod = new BigInteger(token.Value<string>(nameof(PrivateModulus)));
			var privExp = new BigInteger(token.Value<string>(nameof(PrivateExponent)));
			var pubMod = new BigInteger(token.Value<string>(nameof(PublicModulus)));
			var pubExp = new BigInteger(token.Value<string>(nameof(PublicExponent)));

			return new BlindingRsaKey(privMod, privExp, pubMod, pubExp);
		}

		#region Equality

		public override bool Equals(object obj) => obj is BlindingRsaKey && this == (BlindingRsaKey)obj;

		public bool Equals(BlindingRsaKey other) => this == other;

		public override int GetHashCode()
		{
			var hash = PrivateModulus.GetHashCode();
			hash = hash ^ PrivateExponent.GetHashCode();
			hash = hash ^ PublicModulus.GetHashCode();
			hash = hash ^ PublicExponent.GetHashCode();

			return hash;
		}

		public static bool operator ==(BlindingRsaKey x, BlindingRsaKey y)
		{
			if (ReferenceEquals(x, y)) return true;
			if ((object)x is null ^ (object)y is null) return false;
			if (x.PrivateExponent is null ^ y.PrivateExponent is null) return false;
			if (x.PublicModulus is null ^ y.PublicModulus is null) return false;
			if (x.PublicExponent is null ^ y.PublicExponent is null) return false;

			return
				x.PrivateModulus.Equals(y.PrivateModulus)
				&& x.PrivateExponent.Equals(y.PrivateExponent)
				&& x.PublicModulus.Equals(y.PublicModulus)
				&& x.PublicExponent.Equals(y.PublicExponent);
		}

		public static bool operator !=(BlindingRsaKey x, BlindingRsaKey y) => !(x == y);

		#endregion Equality
	}
}
