using System.Security.Cryptography;
using NBitcoin.DataEncoders;
using WalletWasabi.Affiliation;
using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Affiliation;

public class SignatureTests
{
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
		CoinJoinRequestRequestsSigner signer = new(Constants.FallbackCoordinatorSignerKeyHex);
	}
}
