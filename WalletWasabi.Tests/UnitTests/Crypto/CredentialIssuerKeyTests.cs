using System.Linq;
using Moq;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto;

public class CredentialIssuerKeyTests
{
	[Theory]
	[InlineData(0, 1, 1, 1, 1)]
	[InlineData(1, 0, 1, 1, 1)]
	[InlineData(1, 1, 0, 1, 1)]
	[InlineData(1, 1, 1, 0, 1)]
	[InlineData(1, 1, 1, 1, 0)]
	public void CannotGenerateIssuerSecretKeyWithZero(int w, int wp, int x0, int x1, int ya)
	{
		var keys = new [] { ("w", w), ("wp", wp), ("x0", x0), ("x1", x1), ("ya", ya) };
		var mockRandom = new Mock<WasabiRandom>();
		var seq = mockRandom.SetupSequence(rnd => rnd.GetScalar());
		Array.ForEach(keys, k => seq.Returns(k.Item2 == 0 ? Scalar.Zero : Scalar.One));

		var ex = Assert.Throws<ArgumentException>(() => new CredentialIssuerSecretKey(mockRandom.Object));
		Assert.StartsWith($"Value cannot be zero. (Parameter '{keys.Single(k => k.Item2 == 0).Item1}')", ex.Message);
	}

	[Fact]
	public void GenerateCredentialIssuerParameters()
	{
		var inf = GroupElement.Infinity;
		var g = Generators.G;
		var ex = Assert.Throws<ArgumentException>(() => new CredentialIssuerParameters(inf, g));
		Assert.StartsWith("Point at infinity is not a valid value.", ex.Message);

		ex = Assert.Throws<ArgumentException>(() => new CredentialIssuerParameters(g, inf));
		Assert.StartsWith("Point at infinity is not a valid value.", ex.Message);
	}
}
