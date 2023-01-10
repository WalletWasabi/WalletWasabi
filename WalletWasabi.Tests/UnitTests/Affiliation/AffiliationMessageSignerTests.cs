using System.Security.Cryptography;
using NBitcoin.DataEncoders;
using WalletWasabi.Affiliation;
using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Affiliation;

public class AffiliationMessageSignerTests
{
	/// <summary>The method shows how to generate affiliation message signer key</summary>
	public (string, string) GenerateKey()
	{
		var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var privateKeyBytes = ecdsa.ExportECPrivateKey();
		var publicKeyBytes = ecdsa.ExportSubjectPublicKeyInfo();
		var privateKeyHex = Encoders.Hex.EncodeData(privateKeyBytes);
		var publicKeyHex = Encoders.Hex.EncodeData(publicKeyBytes);
		return (privateKeyHex, publicKeyHex);
	}

	[Fact]
	public void FallbackSignerKeyTest()
	{
		using AffiliationMessageSigner signer = new(Constants.FallbackAffiliationMessageSignerKey);
	}

	[Fact]
	public void GeneratedKeyTest()
	{
		(string privateKey, string publicKey) = GenerateKey();
		using AffiliationMessageSigner signer = new(privateKey);
	}
}
