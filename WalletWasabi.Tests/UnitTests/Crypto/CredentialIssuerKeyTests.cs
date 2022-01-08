using Moq;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto;

public class CredentialIssuerKeyTests
{
	[Fact]
	public void GenerateCredentialIssuerParameters()
	{
		// Coordinator key is (0, 0, 0, 0, 0)
		var mockRandom = new Mock<WasabiRandom>();
		mockRandom.Setup(rnd => rnd.GetScalar(true)).Returns(Scalar.Zero);
		var key = new CredentialIssuerSecretKey(mockRandom.Object);
		var ex = Assert.Throws<ArgumentException>(key.ComputeCredentialIssuerParameters);
		Assert.StartsWith("Point at infinity is not a valid value.", ex.Message);

		// Coordinator key is (0, 0, 1, 1, 1)
		mockRandom.SetupSequence(rnd => rnd.GetScalar(true))
			.Returns(Scalar.Zero)
			.Returns(Scalar.Zero)
			.Returns(Scalar.One)
			.Returns(Scalar.One)
			.Returns(Scalar.One);
		key = new CredentialIssuerSecretKey(mockRandom.Object);
		ex = Assert.Throws<ArgumentException>(key.ComputeCredentialIssuerParameters);
		Assert.StartsWith("Point at infinity is not a valid value.", ex.Message);

		// Coordinator key is (1, 1, 0, 0, 0)
		mockRandom.SetupSequence(rnd => rnd.GetScalar(true))
			.Returns(Scalar.One)
			.Returns(Scalar.One)
			.Returns(Scalar.Zero)
			.Returns(Scalar.Zero)
			.Returns(Scalar.Zero);
		key = new CredentialIssuerSecretKey(mockRandom.Object);
		var iparams = key.ComputeCredentialIssuerParameters();
		Assert.Equal(Generators.GV, iparams.I);

		// Coordinator key is (1, 1, 1, 1, 1)
		mockRandom.Setup(rnd => rnd.GetScalar(true))
			.Returns(Scalar.One);
		key = new CredentialIssuerSecretKey(mockRandom.Object);
		iparams = key.ComputeCredentialIssuerParameters();
		Assert.Equal(Generators.Gw + Generators.Gwp, iparams.Cw);
		Assert.Equal(Generators.GV - Generators.Gx0 - Generators.Gx1 - Generators.Ga, iparams.I);
	}
}
