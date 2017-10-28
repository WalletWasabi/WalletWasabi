using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Text;

namespace HiddenWallet.Crypto
{
	public class BlindingRsaKey
    {
		public AsymmetricCipherKeyPair KeyPair { get; private set; }
		public BlindingRsaPubKey PubKey { get; private set; }

		public BlindingRsaKey()
		{
			// Generate a 2048-bit RSA key pair.
			var generator = new RsaKeyPairGenerator();
			var RSA_F4 = BigInteger.ValueOf(65537);
			generator.Init(new RsaKeyGenerationParameters(
						publicExponent: RSA_F4,
						random: new SecureRandom(),
						strength: 2048,
						certainty: 100)); // See A.15.2 IEEE P1363 v2 D1 for certainty parameter
			KeyPair = generator.GenerateKeyPair();
			PubKey = new BlindingRsaPubKey((RsaKeyParameters)KeyPair.Public);
		}

		/// <returns>signature</returns>
		public byte[] SignBlindedData(byte[] blindedData)
		{
			var signer = new RsaEngine();
			signer.Init(forEncryption: true, parameters: KeyPair.Private);
			return signer.ProcessBlock(blindedData, 0, blindedData.Length);
		}
	}
}
