using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Text;

namespace HiddenWallet.Crypto
{
    public class BlindingRsaPubKey
    {
		public RsaKeyParameters KeyParameters { get; private set; }

		public BlindingRsaPubKey(RsaKeyParameters keyParameters)
		{
			KeyParameters = keyParameters ?? throw new ArgumentNullException(nameof(keyParameters));
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
				digest: new Sha1Digest(),
				saltLen: 20);
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
				digest: new Sha1Digest(),
				saltLen: 20);
			verifier.Init(forSigning: false, parameters: KeyParameters);
			verifier.BlockUpdate(data, 0, data.Length);
			return verifier.VerifySignature(signature);
		}
	}
}
