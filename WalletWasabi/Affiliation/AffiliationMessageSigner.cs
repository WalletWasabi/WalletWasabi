using NBitcoin.DataEncoders;
using System.Security.Cryptography;

namespace WalletWasabi.Affiliation;

public class AffiliationMessageSigner : IDisposable
{
	public AffiliationMessageSigner(string signingKeyHex)
	{
		byte[] signingKeyBytes = Encoders.Hex.DecodeData(signingKeyHex);
		ECDsa = ECDsa.Create();
		ECDsa.ImportECPrivateKey(signingKeyBytes, out int readBytes);

		if (signingKeyBytes.Length != readBytes)
		{
			throw new ArgumentException("Invalid key.", nameof(signingKeyHex));
		}

		// ECDSA_P256
		if (ECDsa.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7")
		{
			throw new NotSupportedException("Unsupported curve.");
		}
	}

	private ECDsa ECDsa { get; }

	public byte[] Sign(byte[] digest)
	{
		return ECDsa.SignData(digest, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
	}

	public void Dispose()
	{
		ECDsa.Dispose();
	}
}
